﻿using System.Text;
using System.Text.Json;
using AIDotNet.Abstractions;
using AIDotNet.Abstractions.Dto;
using AIDotNet.Abstractions.Exceptions;
using AIDotNet.Abstractions.ObjectModels.ObjectModels.RequestModels;
using AIDotNet.Abstractions.ObjectModels.ObjectModels.ResponseModels;
using AIDotNet.API.Service.Domain;
using AIDotNet.API.Service.Exceptions;
using AIDotNet.API.Service.Infrastructure;
using AIDotNet.API.Service.Infrastructure.Helper;
using Claudia;
using MapsterMapper;
using Microsoft.IdentityModel.Tokens;
using SkiaSharp;
using CompletionCreateResponse = AIDotNet.Abstractions.Dto.CompletionCreateResponse;

namespace AIDotNet.API.Service.Service;

public sealed class ChatService(
    IServiceProvider serviceProvider,
    ChannelService channelService,
    TokenService tokenService,
    ImageService imageService,
    UserService userService,
    IHttpClientFactory httpClientFactory,
    IMapper mapper,
    LoggerService loggerService)
    : ApplicationService(serviceProvider)
{
    private const string ConsumerTemplate = "模型倍率：{0} 补全倍率：{1}";


    static readonly Dictionary<string, Dictionary<string, double>> ImageSizeRatios = new()
    {
        {
            "dall-e-2", new Dictionary<string, double>
            {
                { "256x256", 1 },
                { "512x512", 1.125 },
                { "1024x1024", 1.25 }
            }
        },
        {
            "dall-e-3", new Dictionary<string, double>
            {
                { "1024x1024", 1 },
                { "1024x1792", 2 },
                { "1792x1024", 2 }
            }
        },
        {
            "ali-stable-diffusion-xl", new Dictionary<string, double>
            {
                { "512x1024", 1 },
                { "1024x768", 1 },
                { "1024x1024", 1 },
                { "576x1024", 1 },
                { "1024x576", 1 }
            }
        },
        {
            "ali-stable-diffusion-v1.5", new Dictionary<string, double>
            {
                { "512x1024", 1 },
                { "1024x768", 1 },
                { "1024x1024", 1 },
                { "576x1024", 1 },
                { "1024x576", 1 }
            }
        },
        {
            "wanx-v1", new Dictionary<string, double>
            {
                { "1024x1024", 1 },
                { "720x1280", 1 },
                { "1280x720", 1 }
            }
        }
    };

    public async ValueTask ImageAsync(HttpContext context)
    {
        try
        {
            var (token, user) = await tokenService.CheckTokenAsync(context);

            using var body = new MemoryStream();
            await context.Request.Body.CopyToAsync(body);

            var module = JsonSerializer.Deserialize<ImageCreateRequest>(body.ToArray());

            var imageCostRatio = GetImageCostRatio(module);

            var rate = SettingService.PromptRate[module.Model];

            var quota = (int)(rate * imageCostRatio * 1000) * module.N;

            if (module == null)
            {
                throw new Exception("模型校验异常");
            }

            if (quota > user.ResidualCredit)
            {
                throw new InsufficientQuotaException("额度不足");
            }

            // 获取渠道 通过算法计算权重
            var channel = CalculateWeight((await channelService.GetChannelsAsync())
                .Where(x => x.Models.Contains(module.Model)));

            if (channel == null)
            {
                throw new NotModelException(module.Model);
            }


            // 获取渠道指定的实现类型的服务
            var openService = GetKeyedService<IApiImageService>(channel.Type);

            if (openService == null)
            {
                throw new Exception("渠道服务不存在");
            }

            var response = await openService.CreateImage(module, new ChatOptions()
            {
                Key = channel.Key,
                Address = channel.Address,
            }, context.RequestAborted);

            await context.Response.WriteAsJsonAsync(new AIDotNetImageCreateResponse()
            {
                data = response.Results,
                created = response.CreatedAt,
                successful = response.Successful
            });


            await loggerService.CreateConsumeAsync(string.Format(ConsumerTemplate, rate, 0), module.Model,
                0, 0, quota ?? 0, token?.Name, user?.UserName, user?.Id, channel.Id,
                channel.Name);

            await userService.ConsumeAsync(user!.Id, quota ?? 0, 0, token?.Key);

        }
        catch (UnauthorizedAccessException e)
        {
        }
        catch (Exception e)
        {
            GetLogger<ChatService>().LogError(e.Message);
            await context.WriteErrorAsync(e.Message);
        }
    }

    public async ValueTask EmbeddingAsync(HttpContext context)
    {
        try
        {
            var (token, user) = await tokenService.CheckTokenAsync(context);

            using var body = new MemoryStream();
            await context.Request.Body.CopyToAsync(body);

            var module = JsonSerializer.Deserialize<EmbeddingInput>(body.ToArray());

            if (module == null)
            {
                throw new Exception("模型校验异常");
            }

            // 获取渠道 通过算法计算权重
            var channel = CalculateWeight((await channelService.GetChannelsAsync())
                .Where(x => x.Models.Contains(module.Model)));

            if (channel == null)
            {
                throw new NotModelException(module.Model);
            }

            // 获取渠道指定的实现类型的服务
            var openService = GetKeyedService<IApiTextEmbeddingGeneration>(channel.Type);

            if (openService == null)
            {
                throw new Exception("渠道服务不存在");
            }

            var embeddingCreateRequest = new EmbeddingCreateRequest()
            {
                Model = module.Model,
                EncodingFormat = module.EncodingFormat,
            };

            int requestToken;
            if (module.Input is JsonElement str)
            {
                if (str.ValueKind == JsonValueKind.String)
                {
                    embeddingCreateRequest.Input = str.ToString();
                    requestToken = TokenHelper.GetTotalTokens(str.ToString());
                }
                else if (str.ValueKind == JsonValueKind.Array)
                {
                    var inputString = str.EnumerateArray().Select(x => x.ToString()).ToArray();
                    embeddingCreateRequest.InputAsList = inputString.ToList();
                    requestToken = TokenHelper.GetTotalTokens(inputString);
                }
                else
                {
                    throw new Exception("输入格式错误");
                }
            }
            else
            {
                throw new Exception("输入格式错误");
            }

            var stream = await openService.EmbeddingAsync(embeddingCreateRequest, new ChatOptions()
            {
                Key = channel.Key,
                Address = channel.Address,
            }, context.RequestAborted);

            if (SettingService.PromptRate.TryGetValue(module.Model, out var rate))
            {
                var quota = requestToken * rate;

                var completionRatio = GetCompletionRatio(module.Model);
                quota += (rate * completionRatio);

                // 将quota 四舍五入
                quota = Math.Round(quota, 0, MidpointRounding.AwayFromZero);

                await loggerService.CreateConsumeAsync(string.Format(ConsumerTemplate, rate, completionRatio),
                    module.Model,
                    requestToken, 0, (int)quota, token?.Name, user?.UserName, user?.Id, channel.Id,
                    channel.Name);

                await userService.ConsumeAsync(user!.Id, (long)quota, requestToken, token?.Key);

            }

            await context.Response.WriteAsJsonAsync(stream);
        }
        catch (UnauthorizedAccessException e)
        {
        }
        catch (Exception e)
        {
            GetLogger<ChatService>().LogError(e.Message);
            await context.WriteErrorAsync(e.Message);
        }
    }

    public async ValueTask CompletionsAsync(HttpContext context)
    {
        using var body = new MemoryStream();
        await context.Request.Body.CopyToAsync(body);

        var module = JsonSerializer.Deserialize<ChatCompletionCreateRequest>(body.ToArray());

        if (module == null)
        {
            throw new Exception("模型校验异常");
        }

        try
        {
            var (token, user) = await tokenService.CheckTokenAsync(context);

            // 获取渠道 通过算法计算权重
            var channel = CalculateWeight((await channelService.GetChannelsAsync())
                .Where(x => x.Models.Contains(module.Model)));

            if (channel == null)
            {
                throw new NotModelException(module.Model);
            }

            // 获取渠道指定的实现类型的服务
            var openService = GetKeyedService<IApiChatCompletionService>(channel.Type);

            if (openService == null)
            {
                throw new Exception("渠道服务不存在");
            }

            if (SettingService.PromptRate.TryGetValue(module.Model, out var rate))
            {
                int requestToken;
                int responseToken = 0;

                if (module.Stream == true)
                {
                    (requestToken, responseToken) = await StreamHandlerAsync(context, module, channel, openService);
                }
                else
                {
                    (requestToken, responseToken) = await ChatHandlerAsync(context, module, channel, openService);
                }

                var quota = requestToken * rate;

                var completionRatio = GetCompletionRatio(module.Model);
                quota += responseToken * (rate * completionRatio);

                // 将quota 四舍五入
                quota = Math.Round(quota, 0, MidpointRounding.AwayFromZero);

                await loggerService.CreateConsumeAsync(string.Format(ConsumerTemplate, rate, completionRatio),
                    module.Model,
                    requestToken, responseToken, (int)quota, token?.Name, user?.UserName, user?.Id, channel.Id,
                    channel.Name);

                await userService.ConsumeAsync(user!.Id, (long)quota, requestToken, token?.Key);
            }
        }
        catch (UnauthorizedAccessException e)
        {
        }
        catch (Exception e)
        {
            GetLogger<ChatService>().LogError(e.Message);
            context.Response.StatusCode = 200;
            if (module.Stream == true)
            {
                await context.WriteStreamErrorAsync(e.Message);
            }
            else
            {
                await context.WriteErrorAsync(e.Message);
            }
        }
    }

    private async ValueTask<(int, int)> ChatHandlerAsync(HttpContext context, ChatCompletionCreateRequest input,
        ChatChannel channel, IApiChatCompletionService openService)
    {
        int requestToken;
        int responseToken;

        var setting = new ChatOptions()
        {
            Key = channel.Key,
            Address = channel.Address,
        };

        // 这里应该用其他的方式来判断是否是vision模型，目前先这样处理
        if (input.Model?.Contains("vision") == true)
        {
            requestToken = TokenHelper.GetTotalTokens(input?.Messages.SelectMany(x => x.Contents)
                .Where(x => x.Type == "text").Select(x => x.Text).ToArray());

            // 解析图片
            foreach (var message in input.Messages.SelectMany(x => x.Contents).Where(x => x.Type == "image"))
            {
                if (message.Type == "image_url")
                {
                    var imageUrl = message.ImageUrl;
                    if (imageUrl != null)
                    {
                        var url = imageUrl.Url;
                        var detail = "";
                        if (!imageUrl.Detail.IsNullOrEmpty())
                        {
                            detail = imageUrl.Detail;
                        }

                        try
                        {
                            var imageTokens = await CountImageTokens(url, detail);
                            requestToken += imageTokens.Item1;
                        }
                        catch (Exception ex)
                        {
                            GetLogger<ChatService>().LogError("Error counting image tokens: " + ex.Message);
                        }
                    }
                }
            }

            var result = await openService.CompleteChatAsync(input, setting);

            await context.Response.WriteAsJsonAsync(mapper.Map<CompletionCreateResponse>(result));

            responseToken = TokenHelper.GetTokens(result.Choices.FirstOrDefault()?.Delta.Content ?? string.Empty);
        }
        else
        {
            requestToken = TokenHelper.GetTotalTokens(input?.Messages.Select(x => x.Content).ToArray());


            var result = await openService.CompleteChatAsync(input, setting);

            await context.Response.WriteAsJsonAsync(mapper.Map<CompletionCreateResponse>(result));

            responseToken = TokenHelper.GetTokens(result.Choices.FirstOrDefault()?.Delta.Content ?? string.Empty);
        }

        return (requestToken, responseToken);
    }

    /// <summary>
    /// Stream 对话处理
    /// </summary>
    /// <param Name="context"></param>
    /// <param Name="body"></param>
    /// <param Name="module"></param>
    /// <param Name="channel"></param>
    /// <param Name="openService"></param>
    /// <param name="context"></param>
    /// <param name="input">输入</param>
    /// <param name="channel">渠道</param>
    /// <param name="openService"></param>
    /// <returns></returns>
    private async ValueTask<(int, int)> StreamHandlerAsync(HttpContext context,
        ChatCompletionCreateRequest input, ChatChannel channel, IApiChatCompletionService openService)
    {
        int requestToken;

        var setting = new ChatOptions()
        {
            Key = channel.Key,
            Address = channel.Address,
        };

        var responseMessage = new StringBuilder();

        context.Response.Headers.ContentType = "text/event-stream";

        if (input.Model?.Contains("vision") == true)
        {
            foreach (var message in input.Messages)
            {
                if (message.Content?.StartsWith('[') == true && message.Content?.EndsWith(']') == true)
                {
                    message.Contents = JsonSerializer.Deserialize<List<MessageContent>>(message.Content);
                    // 俩个不能同时存在
                    message.Content = null;
                }
            }

            requestToken = TokenHelper.GetTotalTokens(input?.Messages.Where(x => x is { Contents: not null })
                .SelectMany(x => x!.Contents)
                .Where(x => x.Type == "text")
                .Select(x => x.Text).ToArray());

            // 解析图片
            foreach (var message in input.Messages.Where(x => x is { Contents: not null }).SelectMany(x => x.Contents)
                         .Where(x => x.Type == "image_url"))
            {
                if (message.Type == "image_url")
                {
                    var imageUrl = message.ImageUrl;
                    if (imageUrl != null)
                    {
                        var url = imageUrl.Url;
                        var detail = "";
                        if (!imageUrl.Detail.IsNullOrEmpty())
                        {
                            detail = imageUrl.Detail;
                        }

                        try
                        {
                            var imageTokens = await CountImageTokens(url, detail);
                            requestToken += imageTokens.Item1;
                        }
                        catch (Exception ex)
                        {
                            GetLogger<ChatService>().LogError("Error counting image tokens: " + ex.Message);
                        }
                    }
                }
            }

            await foreach (var item in openService.StreamChatAsync(input, setting))
            {
                responseMessage.Append(item.Choices?.FirstOrDefault()?.Delta.Content ?? string.Empty);
                await context.WriteResultAsync(item);
            }

            await context.WriteEndAsync();
        }
        else
        {
            requestToken = TokenHelper.GetTotalTokens(input?.Messages.Select(x => x.Content).ToArray());

            await foreach (var item in openService.StreamChatAsync(input, setting))
            {
                responseMessage.Append(item.Choices?.FirstOrDefault()?.Delta.Content ?? string.Empty);
                await context.WriteResultAsync(item);
            }

            await context.WriteEndAsync();
        }

        var responseToken = TokenHelper.GetTokens(responseMessage.ToString());

        return (requestToken, responseToken);
    }

    /// <summary>
    /// 权重算法
    /// </summary>
    /// <param Name="channel"></param>
    /// <returns></returns>
    private static ChatChannel CalculateWeight(IEnumerable<ChatChannel> channel)
    {
        // order越大，权重越大，order越小，权重越小，然后随机一个
        var chatChannels = channel as ChatChannel[] ?? channel.ToArray();
        var total = chatChannels.Sum(x => x.Order);

        if (chatChannels.Length == 0)
        {
            throw new NotModelException("没有可用的模型");
        }

        var random = new Random();

        var value = random.Next(0, total);

        var result = chatChannels.First(x =>
        {
            value -= x.Order;
            return value <= 0;
        });

        return result;
    }

    /// <summary>
    /// 对话模型补全倍率
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    private static decimal GetCompletionRatio(string name)
    {
        if (SettingService.CompletionRate.TryGetValue(name, out var ratio))
        {
            return ratio;
        }

        if (name.StartsWith("gpt-3.5"))
        {
            if (name == "gpt-3.5-turbo" || name.EndsWith("0125"))
            {
                return 3;
            }

            if (name.EndsWith("1106"))
            {
                return 2;
            }

            return (decimal)(4.0 / 3.0);
        }

        if (name.StartsWith("gpt-4"))
        {
            return name.StartsWith("gpt-4-turbo") ? 3 : 2;
        }

        if (name.StartsWith("claude-"))
        {
            return name.StartsWith("claude-3") ? 5 : 3;
        }

        if (name.StartsWith("mistral-") || name.StartsWith("gemini-"))
        {
            return 3;
        }

        return name switch
        {
            "llama2-70b-4096" => new decimal(0.8 / 0.7),
            _ => 1
        };
    }

    /// <summary>
    /// 计算图片倍率
    /// </summary>
    /// <param name="module"></param>
    /// <returns></returns>
    private static decimal GetImageCostRatio(ImageCreateRequest module)
    {
        var imageCostRatio = GetImageSizeRatio(module.Model, module.Size);
        if (module is { Quality: "hd", Model: "dall-e-3" })
        {
            if (module.Size == "1024x1024")
            {
                imageCostRatio *= 2;
            }
            else
            {
                imageCostRatio *= (decimal)1.5;
            }
        }

        return imageCostRatio;
    }

    /// <summary>
    /// 计算图片倍率
    /// </summary>
    /// <param name="model"></param>
    /// <param name="size"></param>
    /// <returns></returns>
    public static decimal GetImageSizeRatio(string model, string size)
    {
        if (!ImageSizeRatios.TryGetValue(model, out var ratios)) return 1;

        if (ratios.TryGetValue(size, out var ratio))
        {
            return (decimal)ratio;
        }

        return 1;
    }

    /// <summary>
    /// 计算图片token
    /// </summary>
    /// <param name="url"></param>
    /// <param name="detail"></param>
    /// <returns></returns>
    public async ValueTask<Tuple<int, Exception>> CountImageTokens(string url, string detail)
    {
        bool fetchSize = true;
        int width = 0, height = 0;
        int lowDetailCost = 20; // Assuming lowDetailCost is 20
        int highDetailCostPerTile = 100; // Assuming highDetailCostPerTile is 100
        int additionalCost = 50; // Assuming additionalCost is 50

        if (string.IsNullOrEmpty(detail) || detail == "auto")
        {
            detail = "high";
        }

        switch (detail)
        {
            case "low":
                return new Tuple<int, Exception>(lowDetailCost, null);
            case "high":
                if (fetchSize)
                {
                    try
                    {
                        (width, height) = await imageService.GetImageSize(url);
                    }
                    catch (Exception e)
                    {
                        return new Tuple<int, Exception>(0, e);
                    }
                }

                if (width > 2048 || height > 2048)
                {
                    double ratio = 2048.0 / Math.Max(width, height);
                    width = (int)(width * ratio);
                    height = (int)(height * ratio);
                }

                if (width > 768 && height > 768)
                {
                    double ratio = 768.0 / Math.Min(width, height);
                    width = (int)(width * ratio);
                    height = (int)(height * ratio);
                }

                int numSquares = (int)Math.Ceiling((double)width / 512) * (int)Math.Ceiling((double)height / 512);
                int result = numSquares * highDetailCostPerTile + additionalCost;
                return new Tuple<int, Exception>(result, null);
            default:
                return new Tuple<int, Exception>(0, new Exception("Invalid detail option"));
        }
    }
}