﻿// <auto-generated />
using System;
using AIDotNet.API.Service.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace AIDotNet.API.Service.Migrations
{
    [DbContext(typeof(TokenApiDbContext))]
    partial class TokenApiDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "8.0.2");

            modelBuilder.Entity("AIDotNet.API.Service.Domain.ChatChannel", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("TEXT");

                    b.Property<string>("Address")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT");

                    b.Property<string>("Creator")
                        .HasColumnType("TEXT");

                    b.Property<bool>("Disable")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Extension")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Key")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Models")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Modifier")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int>("Order")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Other")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int>("Quota")
                        .HasColumnType("INTEGER");

                    b.Property<long>("RemainQuota")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("ResponseTime")
                        .HasColumnType("TEXT");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("UpdatedAt")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("Creator");

                    b.HasIndex("Name");

                    b.ToTable("Channels");
                });

            modelBuilder.Entity("AIDotNet.API.Service.Domain.Logger", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("CompletionTokens")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Content")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT");

                    b.Property<string>("Creator")
                        .HasColumnType("TEXT");

                    b.Property<string>("ModelName")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Modifier")
                        .HasColumnType("TEXT");

                    b.Property<int>("PromptTokens")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Quota")
                        .HasColumnType("INTEGER");

                    b.Property<string>("TokenName")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int>("Type")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("UpdatedAt")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("Creator");

                    b.HasIndex("TokenName");

                    b.ToTable("Loggers");
                });

            modelBuilder.Entity("AIDotNet.API.Service.Domain.Token", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("AccessedTime")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT");

                    b.Property<string>("Creator")
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("DeletedAt")
                        .HasColumnType("TEXT");

                    b.Property<bool>("Disabled")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("ExpiredTime")
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsDelete")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Key")
                        .IsRequired()
                        .HasMaxLength(42)
                        .HasColumnType("TEXT");

                    b.Property<string>("Modifier")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<long>("RemainQuota")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("UnlimitedExpired")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("UnlimitedQuota")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("UpdatedAt")
                        .HasColumnType("TEXT");

                    b.Property<long>("UsedQuota")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("Creator");

                    b.ToTable("Tokens");
                });

            modelBuilder.Entity("TokenApi.Contract.Domain.User", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT");

                    b.Property<string>("Creator")
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("DeletedAt")
                        .HasColumnType("TEXT");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsDelete")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Modifier")
                        .HasColumnType("TEXT");

                    b.Property<string>("Password")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("PasswordHas")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("UpdatedAt")
                        .HasColumnType("TEXT");

                    b.Property<string>("UserName")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Users");

                    b.HasData(
                        new
                        {
                            Id = "67b806e5-5deb-4f6a-88b1-914487aa0212",
                            CreatedAt = new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                            Email = "239573049@qq.com",
                            IsDelete = false,
                            Password = "f02648e32b1b479bf964b0d33da58780",
                            PasswordHas = "9ad551538f784ce7abd656ce5bab0ae2",
                            UserName = "admin"
                        });
                });
#pragma warning restore 612, 618
        }
    }
}