using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Esertifikasi.Api.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "esertifikasi");

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentType",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Nama = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    OwnerType = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    AllowedExtensions = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MaximumFileSize = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MonitoringDefinition",
                schema: "esertifikasi",
                columns: table => new
                {
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Group = table.Column<int>(type: "integer", nullable: false),
                    Frequency = table.Column<int>(type: "integer", nullable: false),
                    RequiredOccurrencesPerYear = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoringDefinition", x => x.Type);
                });

            migrationBuilder.CreateTable(
                name: "MonitoringReferenceItem",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoringReferenceItem", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Province",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SourceUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Province", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegionDatasetImport",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Source = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DatasetSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProvinceCount = table.Column<int>(type: "integer", nullable: false),
                    RegencyCount = table.Column<int>(type: "integer", nullable: false),
                    DistrictCount = table.Column<int>(type: "integer", nullable: false),
                    VillageCount = table.Column<int>(type: "integer", nullable: false),
                    AppliedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegionDatasetImport", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                schema: "esertifikasi",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                schema: "esertifikasi",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                schema: "esertifikasi",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshToken",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshToken", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshToken_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Regency",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    ProvinceId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SourceUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regency", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Regency_Province_ProvinceId",
                        column: x => x.ProvinceId,
                        principalSchema: "esertifikasi",
                        principalTable: "Province",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "District",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    RegencyId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SourceUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_District", x => x.Id);
                    table.ForeignKey(
                        name: "FK_District_Regency_RegencyId",
                        column: x => x.RegencyId,
                        principalSchema: "esertifikasi",
                        principalTable: "Regency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Village",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    DistrictId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SourceUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Village", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Village_District_DistrictId",
                        column: x => x.DistrictId,
                        principalSchema: "esertifikasi",
                        principalTable: "District",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Association",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nama = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    LevelOrganisasi = table.Column<int>(type: "integer", nullable: false),
                    JenisOrganisasi = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    KetuaOrganisasi = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Bendahara = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SekretarisOrganisasi = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Bidang = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    NoTelp = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ProvinceId = table.Column<long>(type: "bigint", nullable: true),
                    RegencyId = table.Column<long>(type: "bigint", nullable: true),
                    DistrictId = table.Column<long>(type: "bigint", nullable: true),
                    DesaId = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Association", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Association_District_DistrictId",
                        column: x => x.DistrictId,
                        principalSchema: "esertifikasi",
                        principalTable: "District",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Association_Province_ProvinceId",
                        column: x => x.ProvinceId,
                        principalSchema: "esertifikasi",
                        principalTable: "Province",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Association_Regency_RegencyId",
                        column: x => x.RegencyId,
                        principalSchema: "esertifikasi",
                        principalTable: "Regency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Association_Village_DesaId",
                        column: x => x.DesaId,
                        principalSchema: "esertifikasi",
                        principalTable: "Village",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssociationAdminAssignment",
                schema: "esertifikasi",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssociationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssociationAdminAssignment", x => new { x.UserId, x.AssociationId });
                    table.ForeignKey(
                        name: "FK_AssociationAdminAssignment_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssociationAdminAssignment_Association_AssociationId",
                        column: x => x.AssociationId,
                        principalSchema: "esertifikasi",
                        principalTable: "Association",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CertificationCycle",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssociationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentPhase = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TargetAuditStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TargetAuditEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CompletedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificationCycle", x => x.Id);
                    table.CheckConstraint("CK_CertificationCycle_Sequence", "\"SequenceNumber\" >= 0");
                    table.CheckConstraint("CK_CertificationCycle_TypeSequence", "(\"Type\" = 0 AND \"SequenceNumber\" = 0) OR (\"Type\" = 1 AND \"SequenceNumber\" BETWEEN 1 AND 4) OR (\"Type\" = 2 AND \"SequenceNumber\" >= 5)");
                    table.ForeignKey(
                        name: "FK_CertificationCycle_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CertificationCycle_Association_AssociationId",
                        column: x => x.AssociationId,
                        principalSchema: "esertifikasi",
                        principalTable: "Association",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Poktan",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssociationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nama = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    JumlahPetaniPekebunSwadaya = table.Column<int>(type: "integer", nullable: true),
                    BatasMaksimumLuasSawit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    NomorKeanggotaanRspo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Negara = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProvinceId = table.Column<long>(type: "bigint", nullable: true),
                    RegencyId = table.Column<long>(type: "bigint", nullable: true),
                    LuasAreaSawit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Poktan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Poktan_Association_AssociationId",
                        column: x => x.AssociationId,
                        principalSchema: "esertifikasi",
                        principalTable: "Association",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Poktan_Province_ProvinceId",
                        column: x => x.ProvinceId,
                        principalSchema: "esertifikasi",
                        principalTable: "Province",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Poktan_Regency_RegencyId",
                        column: x => x.RegencyId,
                        principalSchema: "esertifikasi",
                        principalTable: "Regency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CertificationAudit",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificationCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ScheduledDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PerformedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FindingsDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    HasFindings = table.Column<bool>(type: "boolean", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResultNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificationAudit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CertificationAudit_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CertificationAudit_CertificationCycle_CertificationCycleId",
                        column: x => x.CertificationCycleId,
                        principalSchema: "esertifikasi",
                        principalTable: "CertificationCycle",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CycleDocumentRequirement",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificationCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerType = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    RequiredCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CycleDocumentRequirement", x => x.Id);
                    table.CheckConstraint("CK_CycleDocumentRequirement_Count", "\"RequiredCount\" > 0");
                    table.ForeignKey(
                        name: "FK_CycleDocumentRequirement_CertificationCycle_CertificationCy~",
                        column: x => x.CertificationCycleId,
                        principalSchema: "esertifikasi",
                        principalTable: "CertificationCycle",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CycleDocumentRequirement_DocumentType_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalSchema: "esertifikasi",
                        principalTable: "DocumentType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CycleStepProgress",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificationCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Step = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequiredTarget = table.Column<int>(type: "integer", nullable: true),
                    AuditPerformedPercentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CycleStepProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CycleStepProgress_CertificationCycle_CertificationCycleId",
                        column: x => x.CertificationCycleId,
                        principalSchema: "esertifikasi",
                        principalTable: "CertificationCycle",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Disclosure",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificationCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Disclosure", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Disclosure_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Disclosure_AspNetUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Disclosure_CertificationCycle_CertificationCycleId",
                        column: x => x.CertificationCycleId,
                        principalSchema: "esertifikasi",
                        principalTable: "CertificationCycle",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrainingSession",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificationCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingSession", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingSession_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrainingSession_CertificationCycle_CertificationCycleId",
                        column: x => x.CertificationCycleId,
                        principalSchema: "esertifikasi",
                        principalTable: "CertificationCycle",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTransition",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificationCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromPhase = table.Column<int>(type: "integer", nullable: false),
                    ToPhase = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTransition", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowTransition_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowTransition_CertificationCycle_CertificationCycleId",
                        column: x => x.CertificationCycleId,
                        principalSchema: "esertifikasi",
                        principalTable: "CertificationCycle",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IcsAuditorAssignment",
                schema: "esertifikasi",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PoktanId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IcsAuditorAssignment", x => new { x.UserId, x.PoktanId });
                    table.ForeignKey(
                        name: "FK_IcsAuditorAssignment_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IcsAuditorAssignment_Poktan_PoktanId",
                        column: x => x.PoktanId,
                        principalSchema: "esertifikasi",
                        principalTable: "Poktan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MonitoringSubmission",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssociationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PoktanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Group = table.Column<int>(type: "integer", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinalizedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    FinalizedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReopenedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReopenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReopenReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PreparedByName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    AcknowledgedByName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoringSubmission", x => x.Id);
                    table.CheckConstraint("CK_MonitoringSubmission_Period", "\"PeriodEnd\" >= \"PeriodStart\"");
                    table.ForeignKey(
                        name: "FK_MonitoringSubmission_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonitoringSubmission_AspNetUsers_FinalizedByUserId",
                        column: x => x.FinalizedByUserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonitoringSubmission_AspNetUsers_ReopenedByUserId",
                        column: x => x.ReopenedByUserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonitoringSubmission_Association_AssociationId",
                        column: x => x.AssociationId,
                        principalSchema: "esertifikasi",
                        principalTable: "Association",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonitoringSubmission_Poktan_PoktanId",
                        column: x => x.PoktanId,
                        principalSchema: "esertifikasi",
                        principalTable: "Poktan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Petani",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PoktanId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Nama = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Nik = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    TempatLahir = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    TanggalLahir = table.Column<DateOnly>(type: "date", nullable: true),
                    JenisKelamin = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DesaId = table.Column<long>(type: "bigint", nullable: true),
                    RtRw = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Alamat = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StatusPerkawinan = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Pekerjaan = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Kewarganegaraan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Suku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    NoTelepon = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    NoKk = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    NamaKepalaKeluarga = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    JumlahAnggotaKeluarga = table.Column<int>(type: "integer", nullable: true),
                    JumlahAnak = table.Column<int>(type: "integer", nullable: true),
                    PekerjaanSampingan = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PendidikanTerakhir = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Petani", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Petani_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Petani_Poktan_PoktanId",
                        column: x => x.PoktanId,
                        principalSchema: "esertifikasi",
                        principalTable: "Poktan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Petani_Village_DesaId",
                        column: x => x.DesaId,
                        principalSchema: "esertifikasi",
                        principalTable: "Village",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PoktanAdminAssignment",
                schema: "esertifikasi",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PoktanId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoktanAdminAssignment", x => new { x.UserId, x.PoktanId });
                    table.ForeignKey(
                        name: "FK_PoktanAdminAssignment_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PoktanAdminAssignment_Poktan_PoktanId",
                        column: x => x.PoktanId,
                        principalSchema: "esertifikasi",
                        principalTable: "Poktan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditFinding",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificationAuditId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    PoktanId = table.Column<Guid>(type: "uuid", nullable: true),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    PenyebabAnalisis = table.Column<string>(type: "text", nullable: true),
                    Corrections = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CorrectiveAction = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ClosureEvidence = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditFinding", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditFinding_AspNetUsers_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditFinding_CertificationAudit_CertificationAuditId",
                        column: x => x.CertificationAuditId,
                        principalSchema: "esertifikasi",
                        principalTable: "CertificationAudit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuditFinding_Poktan_PoktanId",
                        column: x => x.PoktanId,
                        principalSchema: "esertifikasi",
                        principalTable: "Poktan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChemicalBufferMonitoring",
                schema: "esertifikasi",
                columns: table => new
                {
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChemicalBufferMonitoring", x => x.MonitoringSubmissionId);
                    table.ForeignKey(
                        name: "FK_ChemicalBufferMonitoring_MonitoringSubmission_MonitoringSub~",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "MonitoringSubmission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FireMonitoring",
                schema: "esertifikasi",
                columns: table => new
                {
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    NoIncidents = table.Column<bool>(type: "boolean", nullable: false),
                    ZeroIncidentDeclaration = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FireMonitoring", x => x.MonitoringSubmissionId);
                    table.ForeignKey(
                        name: "FK_FireMonitoring_MonitoringSubmission_MonitoringSubmissionId",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "MonitoringSubmission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FirstAidKitMonitoring",
                schema: "esertifikasi",
                columns: table => new
                {
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirstAidKitMonitoring", x => x.MonitoringSubmissionId);
                    table.ForeignKey(
                        name: "FK_FirstAidKitMonitoring_MonitoringSubmission_MonitoringSubmis~",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "MonitoringSubmission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HighConservationValueMonitoring",
                schema: "esertifikasi",
                columns: table => new
                {
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HighConservationValueMonitoring", x => x.MonitoringSubmissionId);
                    table.ForeignKey(
                        name: "FK_HighConservationValueMonitoring_MonitoringSubmission_Monito~",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "MonitoringSubmission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LandBoundaryMonitoring",
                schema: "esertifikasi",
                columns: table => new
                {
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandBoundaryMonitoring", x => x.MonitoringSubmissionId);
                    table.ForeignKey(
                        name: "FK_LandBoundaryMonitoring_MonitoringSubmission_MonitoringSubmi~",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "MonitoringSubmission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemberComplaintMonitoring",
                schema: "esertifikasi",
                columns: table => new
                {
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    NoComplaints = table.Column<bool>(type: "boolean", nullable: false),
                    ZeroComplaintDeclaration = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberComplaintMonitoring", x => x.MonitoringSubmissionId);
                    table.ForeignKey(
                        name: "FK_MemberComplaintMonitoring_MonitoringSubmission_MonitoringSu~",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "MonitoringSubmission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MonitoringAttachment",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileExtension = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoringAttachment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonitoringAttachment_AspNetUsers_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonitoringAttachment_MonitoringSubmission_MonitoringSubmiss~",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "MonitoringSubmission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MonitoringFollowUp",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoringFollowUp", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonitoringFollowUp_AspNetUsers_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonitoringFollowUp_MonitoringSubmission_MonitoringSubmissio~",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "MonitoringSubmission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PestMonitoring",
                schema: "esertifikasi",
                columns: table => new
                {
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PestMonitoring", x => x.MonitoringSubmissionId);
                    table.ForeignKey(
                        name: "FK_PestMonitoring_MonitoringSubmission_MonitoringSubmissionId",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "MonitoringSubmission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlantDiseaseMonitoring",
                schema: "esertifikasi",
                columns: table => new
                {
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlantDiseaseMonitoring", x => x.MonitoringSubmissionId);
                    table.ForeignKey(
                        name: "FK_PlantDiseaseMonitoring_MonitoringSubmission_MonitoringSubmi~",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "MonitoringSubmission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PpeMonitoring",
                schema: "esertifikasi",
                columns: table => new
                {
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PpeMonitoring", x => x.MonitoringSubmissionId);
                    table.ForeignKey(
                        name: "FK_PpeMonitoring_MonitoringSubmission_MonitoringSubmissionId",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "MonitoringSubmission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TurneraMonitoring",
                schema: "esertifikasi",
                columns: table => new
                {
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurneraMonitoring", x => x.MonitoringSubmissionId);
                    table.ForeignKey(
                        name: "FK_TurneraMonitoring_MonitoringSubmission_MonitoringSubmission~",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "MonitoringSubmission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WeedMonitoring",
                schema: "esertifikasi",
                columns: table => new
                {
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeedMonitoring", x => x.MonitoringSubmissionId);
                    table.ForeignKey(
                        name: "FK_WeedMonitoring_MonitoringSubmission_MonitoringSubmissionId",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "MonitoringSubmission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WoodyPlantMonitoring",
                schema: "esertifikasi",
                columns: table => new
                {
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WoodyPlantMonitoring", x => x.MonitoringSubmissionId);
                    table.ForeignKey(
                        name: "FK_WoodyPlantMonitoring_MonitoringSubmission_MonitoringSubmiss~",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "MonitoringSubmission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkplaceAccidentMonitoring",
                schema: "esertifikasi",
                columns: table => new
                {
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    NoIncidents = table.Column<bool>(type: "boolean", nullable: false),
                    ZeroIncidentDeclaration = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkplaceAccidentMonitoring", x => x.MonitoringSubmissionId);
                    table.ForeignKey(
                        name: "FK_WorkplaceAccidentMonitoring_MonitoringSubmission_Monitoring~",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "MonitoringSubmission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CertificationParticipant",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificationCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PetaniId = table.Column<Guid>(type: "uuid", nullable: false),
                    PoktanIdSnapshot = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryPath = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StatusReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CertificateEligibilityStatus = table.Column<int>(type: "integer", nullable: false),
                    CertificateEligibilityReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CertificateEligibilityDecidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CertificateEligibilityDecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartingStep = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificationParticipant", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CertificationParticipant_AspNetUsers_CertificateEligibility~",
                        column: x => x.CertificateEligibilityDecidedByUserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CertificationParticipant_CertificationCycle_CertificationCy~",
                        column: x => x.CertificationCycleId,
                        principalSchema: "esertifikasi",
                        principalTable: "CertificationCycle",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CertificationParticipant_Petani_PetaniId",
                        column: x => x.PetaniId,
                        principalSchema: "esertifikasi",
                        principalTable: "Petani",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Lahan",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PetaniId = table.Column<Guid>(type: "uuid", nullable: false),
                    NoLegalitas = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    NoSppl = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    NoStdb = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    JenisKepemilikan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TahunKepemilikan = table.Column<int>(type: "integer", nullable: true),
                    StatusKepemilikan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    KeteranganSertifikat = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    KemanaMenjualPanen = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    NamaPembeli = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    NamaPabrik = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Komoditas = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LuasLegalitas = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    BatasUtara = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    BatasTimur = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    BatasBarat = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    BatasSelatan = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    JenisTanah = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TanamanLain = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    BulanTahunTanam = table.Column<DateOnly>(type: "date", nullable: true),
                    ProduksiRataRata = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TempatBeliPupuk = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    MitraPengelola = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    MitraPengelolaLainnya = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    JumlahPohonPerHa = table.Column<int>(type: "integer", nullable: true),
                    PolaTanam = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AsalBibit = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    JenisBibit = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    BibitBersertifikat = table.Column<bool>(type: "boolean", nullable: true),
                    BisnisLain = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LuasTertanam = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    LuasProduktif = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    RotasiPanen = table.Column<int>(type: "integer", nullable: true),
                    BeratTbs = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DesaId = table.Column<long>(type: "bigint", nullable: true),
                    RtRw = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Alamat = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    BoundaryGeoJson = table.Column<string>(type: "jsonb", nullable: true),
                    NoPetaLahan = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    MetodeBuka = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DibukaOleh = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    TahunDibuka = table.Column<int>(type: "integer", nullable: true),
                    TutupanLahan = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    TutupanLahanLainnya = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PerolehanTanahGarapan = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PeruntukanTanah = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    TanamanAwal = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    StatusHgu = table.Column<bool>(type: "boolean", nullable: true),
                    StatusGambutFeg = table.Column<bool>(type: "boolean", nullable: true),
                    StatusGambutKhg = table.Column<bool>(type: "boolean", nullable: true),
                    LuasGeometri = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    LuasTerverifikasi = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SelisihLuas = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lahan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lahan_Petani_PetaniId",
                        column: x => x.PetaniId,
                        principalSchema: "esertifikasi",
                        principalTable: "Petani",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lahan_Village_DesaId",
                        column: x => x.DesaId,
                        principalSchema: "esertifikasi",
                        principalTable: "Village",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MemberRegistration",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssociationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PoktanId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExistingPetaniId = table.Column<Guid>(type: "uuid", nullable: true),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProviderSubjectId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Nama = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Nik = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectionReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberRegistration", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberRegistration_AspNetUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberRegistration_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MemberRegistration_Association_AssociationId",
                        column: x => x.AssociationId,
                        principalSchema: "esertifikasi",
                        principalTable: "Association",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberRegistration_Petani_ExistingPetaniId",
                        column: x => x.ExistingPetaniId,
                        principalSchema: "esertifikasi",
                        principalTable: "Petani",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberRegistration_Poktan_PoktanId",
                        column: x => x.PoktanId,
                        principalSchema: "esertifikasi",
                        principalTable: "Poktan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrainingAttendance",
                schema: "esertifikasi",
                columns: table => new
                {
                    TrainingSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PetaniId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingAttendance", x => new { x.TrainingSessionId, x.PetaniId });
                    table.ForeignKey(
                        name: "FK_TrainingAttendance_Petani_PetaniId",
                        column: x => x.PetaniId,
                        principalSchema: "esertifikasi",
                        principalTable: "Petani",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrainingAttendance_TrainingSession_TrainingSessionId",
                        column: x => x.TrainingSessionId,
                        principalSchema: "esertifikasi",
                        principalTable: "TrainingSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FirstAidKitInspection",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ObservedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirstAidKitInspection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FirstAidKitInspection_FirstAidKitMonitoring_MonitoringSubmi~",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "FirstAidKitMonitoring",
                        principalColumn: "MonitoringSubmissionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemberComplaint",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PetaniId = table.Column<Guid>(type: "uuid", nullable: true),
                    FarmerNameSnapshot = table.Column<string>(type: "text", nullable: true),
                    NikSnapshot = table.Column<string>(type: "text", nullable: true),
                    ReceivedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ComplaintType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    FollowUp = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ResolvedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    IsAnonymous = table.Column<bool>(type: "boolean", nullable: false),
                    IsConfidential = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberComplaint", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberComplaint_MemberComplaintMonitoring_MonitoringSubmiss~",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "MemberComplaintMonitoring",
                        principalColumn: "MonitoringSubmissionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MemberComplaint_Petani_PetaniId",
                        column: x => x.PetaniId,
                        principalSchema: "esertifikasi",
                        principalTable: "Petani",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DisclosureParticipant",
                schema: "esertifikasi",
                columns: table => new
                {
                    DisclosureId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificationParticipantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisclosureParticipant", x => new { x.DisclosureId, x.CertificationParticipantId });
                    table.ForeignKey(
                        name: "FK_DisclosureParticipant_CertificationParticipant_Certificatio~",
                        column: x => x.CertificationParticipantId,
                        principalSchema: "esertifikasi",
                        principalTable: "CertificationParticipant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DisclosureParticipant_Disclosure_DisclosureId",
                        column: x => x.DisclosureId,
                        principalSchema: "esertifikasi",
                        principalTable: "Disclosure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParticipantStepProgress",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificationParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Step = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ResponsibleUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParticipantStepProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParticipantStepProgress_AspNetUsers_ResponsibleUserId",
                        column: x => x.ResponsibleUserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParticipantStepProgress_CertificationParticipant_Certificat~",
                        column: x => x.CertificationParticipantId,
                        principalSchema: "esertifikasi",
                        principalTable: "CertificationParticipant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BaselineAssessment",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LahanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Result = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Source = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AssessedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaselineAssessment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BaselineAssessment_AspNetUsers_AssessedByUserId",
                        column: x => x.AssessedByUserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BaselineAssessment_Lahan_LahanId",
                        column: x => x.LahanId,
                        principalSchema: "esertifikasi",
                        principalTable: "Lahan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CertificationParticipantLahan",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificationParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LahanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    EntryPath = table.Column<int>(type: "integer", nullable: false),
                    StatusReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificationParticipantLahan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CertificationParticipantLahan_CertificationParticipant_Cert~",
                        column: x => x.CertificationParticipantId,
                        principalSchema: "esertifikasi",
                        principalTable: "CertificationParticipant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CertificationParticipantLahan_Lahan_LahanId",
                        column: x => x.LahanId,
                        principalSchema: "esertifikasi",
                        principalTable: "Lahan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChemicalBufferInspection",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    HasRiverBoundaryMarker = table.Column<bool>(type: "boolean", nullable: false),
                    NoChemicalActivityWithinFiveMeters = table.Column<bool>(type: "boolean", nullable: false),
                    HasWoodyPlantsWithinFiveMeters = table.Column<bool>(type: "boolean", nullable: false),
                    NoPlantingOnSteepSlope = table.Column<bool>(type: "boolean", nullable: false),
                    PetaniId = table.Column<Guid>(type: "uuid", nullable: true),
                    LahanId = table.Column<Guid>(type: "uuid", nullable: true),
                    FarmerNameSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    NikSnapshot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    LandLegalNumberSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LandAreaSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ObservedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FollowUp = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChemicalBufferInspection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChemicalBufferInspection_ChemicalBufferMonitoring_Monitorin~",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "ChemicalBufferMonitoring",
                        principalColumn: "MonitoringSubmissionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChemicalBufferInspection_Lahan_LahanId",
                        column: x => x.LahanId,
                        principalSchema: "esertifikasi",
                        principalTable: "Lahan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChemicalBufferInspection_Petani_PetaniId",
                        column: x => x.PetaniId,
                        principalSchema: "esertifikasi",
                        principalTable: "Petani",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FireIncident",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Chronology = table.Column<string>(type: "text", nullable: false),
                    PetaniId = table.Column<Guid>(type: "uuid", nullable: true),
                    LahanId = table.Column<Guid>(type: "uuid", nullable: true),
                    FarmerNameSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    NikSnapshot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    LandLegalNumberSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LandAreaSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ObservedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FollowUp = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FireIncident", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FireIncident_FireMonitoring_MonitoringSubmissionId",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "FireMonitoring",
                        principalColumn: "MonitoringSubmissionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FireIncident_Lahan_LahanId",
                        column: x => x.LahanId,
                        principalSchema: "esertifikasi",
                        principalTable: "Lahan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FireIncident_Petani_PetaniId",
                        column: x => x.PetaniId,
                        principalSchema: "esertifikasi",
                        principalTable: "Petani",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HcvLocationAssessment",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Semester = table.Column<int>(type: "integer", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: false),
                    PetaniId = table.Column<Guid>(type: "uuid", nullable: true),
                    LahanId = table.Column<Guid>(type: "uuid", nullable: true),
                    FarmerNameSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    NikSnapshot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    LandLegalNumberSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LandAreaSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ObservedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FollowUp = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HcvLocationAssessment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HcvLocationAssessment_HighConservationValueMonitoring_Monit~",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "HighConservationValueMonitoring",
                        principalColumn: "MonitoringSubmissionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HcvLocationAssessment_Lahan_LahanId",
                        column: x => x.LahanId,
                        principalSchema: "esertifikasi",
                        principalTable: "Lahan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HcvLocationAssessment_Petani_PetaniId",
                        column: x => x.PetaniId,
                        principalSchema: "esertifikasi",
                        principalTable: "Petani",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LandBoundaryInspection",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstalledOn = table.Column<DateOnly>(type: "date", nullable: true),
                    MarkerCount = table.Column<int>(type: "integer", nullable: false),
                    Condition = table.Column<int>(type: "integer", nullable: false),
                    PetaniId = table.Column<Guid>(type: "uuid", nullable: true),
                    LahanId = table.Column<Guid>(type: "uuid", nullable: true),
                    FarmerNameSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    NikSnapshot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    LandLegalNumberSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LandAreaSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ObservedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FollowUp = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandBoundaryInspection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandBoundaryInspection_Lahan_LahanId",
                        column: x => x.LahanId,
                        principalSchema: "esertifikasi",
                        principalTable: "Lahan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LandBoundaryInspection_LandBoundaryMonitoring_MonitoringSub~",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "LandBoundaryMonitoring",
                        principalColumn: "MonitoringSubmissionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LandBoundaryInspection_Petani_PetaniId",
                        column: x => x.PetaniId,
                        principalSchema: "esertifikasi",
                        principalTable: "Petani",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MonitoringRecord",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PetaniId = table.Column<Guid>(type: "uuid", nullable: false),
                    LahanId = table.Column<Guid>(type: "uuid", nullable: true),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    MonitoringMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ResponsibleUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoringRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonitoringRecord_AspNetUsers_ResponsibleUserId",
                        column: x => x.ResponsibleUserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonitoringRecord_Lahan_LahanId",
                        column: x => x.LahanId,
                        principalSchema: "esertifikasi",
                        principalTable: "Lahan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonitoringRecord_Petani_PetaniId",
                        column: x => x.PetaniId,
                        principalSchema: "esertifikasi",
                        principalTable: "Petani",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PestInspection",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    PestType = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    ObservedDensity = table.Column<decimal>(type: "numeric", nullable: true),
                    DensityUnit = table.Column<string>(type: "text", nullable: true),
                    Treatment = table.Column<string>(type: "text", nullable: true),
                    PetaniId = table.Column<Guid>(type: "uuid", nullable: true),
                    LahanId = table.Column<Guid>(type: "uuid", nullable: true),
                    FarmerNameSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    NikSnapshot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    LandLegalNumberSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LandAreaSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ObservedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FollowUp = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PestInspection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PestInspection_Lahan_LahanId",
                        column: x => x.LahanId,
                        principalSchema: "esertifikasi",
                        principalTable: "Lahan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PestInspection_PestMonitoring_MonitoringSubmissionId",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "PestMonitoring",
                        principalColumn: "MonitoringSubmissionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PestInspection_Petani_PetaniId",
                        column: x => x.PetaniId,
                        principalSchema: "esertifikasi",
                        principalTable: "Petani",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlantDiseaseInspection",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    DiseaseType = table.Column<string>(type: "text", nullable: false),
                    Result = table.Column<string>(type: "text", nullable: true),
                    Treatment = table.Column<string>(type: "text", nullable: true),
                    PetaniId = table.Column<Guid>(type: "uuid", nullable: true),
                    LahanId = table.Column<Guid>(type: "uuid", nullable: true),
                    FarmerNameSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    NikSnapshot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    LandLegalNumberSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LandAreaSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ObservedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FollowUp = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlantDiseaseInspection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlantDiseaseInspection_Lahan_LahanId",
                        column: x => x.LahanId,
                        principalSchema: "esertifikasi",
                        principalTable: "Lahan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlantDiseaseInspection_Petani_PetaniId",
                        column: x => x.PetaniId,
                        principalSchema: "esertifikasi",
                        principalTable: "Petani",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlantDiseaseInspection_PlantDiseaseMonitoring_MonitoringSub~",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "PlantDiseaseMonitoring",
                        principalColumn: "MonitoringSubmissionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PpeInspection",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Activity = table.Column<int>(type: "integer", nullable: false),
                    PetaniId = table.Column<Guid>(type: "uuid", nullable: true),
                    LahanId = table.Column<Guid>(type: "uuid", nullable: true),
                    FarmerNameSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    NikSnapshot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    LandLegalNumberSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LandAreaSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ObservedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FollowUp = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PpeInspection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PpeInspection_Lahan_LahanId",
                        column: x => x.LahanId,
                        principalSchema: "esertifikasi",
                        principalTable: "Lahan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PpeInspection_Petani_PetaniId",
                        column: x => x.PetaniId,
                        principalSchema: "esertifikasi",
                        principalTable: "Petani",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PpeInspection_PpeMonitoring_MonitoringSubmissionId",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "PpeMonitoring",
                        principalColumn: "MonitoringSubmissionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProtectedSpeciesObservation",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    SpeciesKind = table.Column<int>(type: "integer", nullable: false),
                    SpeciesName = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    PetaniId = table.Column<Guid>(type: "uuid", nullable: true),
                    LahanId = table.Column<Guid>(type: "uuid", nullable: true),
                    FarmerNameSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    NikSnapshot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    LandLegalNumberSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LandAreaSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ObservedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FollowUp = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProtectedSpeciesObservation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProtectedSpeciesObservation_HighConservationValueMonitoring~",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "HighConservationValueMonitoring",
                        principalColumn: "MonitoringSubmissionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProtectedSpeciesObservation_Lahan_LahanId",
                        column: x => x.LahanId,
                        principalSchema: "esertifikasi",
                        principalTable: "Lahan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProtectedSpeciesObservation_Petani_PetaniId",
                        column: x => x.PetaniId,
                        principalSchema: "esertifikasi",
                        principalTable: "Petani",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TurneraInspection",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Condition = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    PetaniId = table.Column<Guid>(type: "uuid", nullable: true),
                    LahanId = table.Column<Guid>(type: "uuid", nullable: true),
                    FarmerNameSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    NikSnapshot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    LandLegalNumberSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LandAreaSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ObservedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FollowUp = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurneraInspection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TurneraInspection_Lahan_LahanId",
                        column: x => x.LahanId,
                        principalSchema: "esertifikasi",
                        principalTable: "Lahan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TurneraInspection_Petani_PetaniId",
                        column: x => x.PetaniId,
                        principalSchema: "esertifikasi",
                        principalTable: "Petani",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TurneraInspection_TurneraMonitoring_MonitoringSubmissionId",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "TurneraMonitoring",
                        principalColumn: "MonitoringSubmissionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WeedInspection",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    WeedType = table.Column<string>(type: "text", nullable: false),
                    Result = table.Column<string>(type: "text", nullable: true),
                    Treatment = table.Column<string>(type: "text", nullable: true),
                    PetaniId = table.Column<Guid>(type: "uuid", nullable: true),
                    LahanId = table.Column<Guid>(type: "uuid", nullable: true),
                    FarmerNameSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    NikSnapshot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    LandLegalNumberSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LandAreaSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ObservedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FollowUp = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeedInspection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeedInspection_Lahan_LahanId",
                        column: x => x.LahanId,
                        principalSchema: "esertifikasi",
                        principalTable: "Lahan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeedInspection_Petani_PetaniId",
                        column: x => x.PetaniId,
                        principalSchema: "esertifikasi",
                        principalTable: "Petani",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeedInspection_WeedMonitoring_MonitoringSubmissionId",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "WeedMonitoring",
                        principalColumn: "MonitoringSubmissionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WoodyPlantInspection",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PetaniId = table.Column<Guid>(type: "uuid", nullable: true),
                    LahanId = table.Column<Guid>(type: "uuid", nullable: true),
                    FarmerNameSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    NikSnapshot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    LandLegalNumberSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LandAreaSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ObservedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FollowUp = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WoodyPlantInspection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WoodyPlantInspection_Lahan_LahanId",
                        column: x => x.LahanId,
                        principalSchema: "esertifikasi",
                        principalTable: "Lahan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WoodyPlantInspection_Petani_PetaniId",
                        column: x => x.PetaniId,
                        principalSchema: "esertifikasi",
                        principalTable: "Petani",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WoodyPlantInspection_WoodyPlantMonitoring_MonitoringSubmiss~",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "WoodyPlantMonitoring",
                        principalColumn: "MonitoringSubmissionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkplaceAccidentIncident",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MonitoringSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    CaseCount = table.Column<int>(type: "integer", nullable: false),
                    Chronology = table.Column<string>(type: "text", nullable: false),
                    PetaniId = table.Column<Guid>(type: "uuid", nullable: true),
                    LahanId = table.Column<Guid>(type: "uuid", nullable: true),
                    FarmerNameSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    NikSnapshot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    LandLegalNumberSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LandAreaSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ObservedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FollowUp = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkplaceAccidentIncident", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkplaceAccidentIncident_Lahan_LahanId",
                        column: x => x.LahanId,
                        principalSchema: "esertifikasi",
                        principalTable: "Lahan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkplaceAccidentIncident_Petani_PetaniId",
                        column: x => x.PetaniId,
                        principalSchema: "esertifikasi",
                        principalTable: "Petani",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkplaceAccidentIncident_WorkplaceAccidentMonitoring_Monit~",
                        column: x => x.MonitoringSubmissionId,
                        principalSchema: "esertifikasi",
                        principalTable: "WorkplaceAccidentMonitoring",
                        principalColumn: "MonitoringSubmissionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FirstAidKitItemInspection",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstAidKitInspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Condition = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    FollowUp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirstAidKitItemInspection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FirstAidKitItemInspection_FirstAidKitInspection_FirstAidKit~",
                        column: x => x.FirstAidKitInspectionId,
                        principalSchema: "esertifikasi",
                        principalTable: "FirstAidKitInspection",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DisclosureLahan",
                schema: "esertifikasi",
                columns: table => new
                {
                    DisclosureId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificationParticipantLahanId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisclosureLahan", x => new { x.DisclosureId, x.CertificationParticipantLahanId });
                    table.ForeignKey(
                        name: "FK_DisclosureLahan_CertificationParticipantLahan_Certification~",
                        column: x => x.CertificationParticipantLahanId,
                        principalSchema: "esertifikasi",
                        principalTable: "CertificationParticipantLahan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DisclosureLahan_Disclosure_DisclosureId",
                        column: x => x.DisclosureId,
                        principalSchema: "esertifikasi",
                        principalTable: "Disclosure",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Document",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PetaniId = table.Column<Guid>(type: "uuid", nullable: true),
                    LahanId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssociationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CertificationCycleId = table.Column<Guid>(type: "uuid", nullable: true),
                    CertificationParticipantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CertificationParticipantLahanId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Catatan = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Document", x => x.Id);
                    table.CheckConstraint("CK_Document_ExactlyOneOwner", "(CASE WHEN \"PetaniId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"LahanId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"AssociationId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"CertificationCycleId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"CertificationParticipantId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"CertificationParticipantLahanId\" IS NULL THEN 0 ELSE 1 END) = 1");
                    table.ForeignKey(
                        name: "FK_Document_AspNetUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Document_AspNetUsers_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Document_Association_AssociationId",
                        column: x => x.AssociationId,
                        principalSchema: "esertifikasi",
                        principalTable: "Association",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Document_CertificationCycle_CertificationCycleId",
                        column: x => x.CertificationCycleId,
                        principalSchema: "esertifikasi",
                        principalTable: "CertificationCycle",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Document_CertificationParticipantLahan_CertificationPartici~",
                        column: x => x.CertificationParticipantLahanId,
                        principalSchema: "esertifikasi",
                        principalTable: "CertificationParticipantLahan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Document_CertificationParticipant_CertificationParticipantId",
                        column: x => x.CertificationParticipantId,
                        principalSchema: "esertifikasi",
                        principalTable: "CertificationParticipant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Document_DocumentType_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalSchema: "esertifikasi",
                        principalTable: "DocumentType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Document_Lahan_LahanId",
                        column: x => x.LahanId,
                        principalSchema: "esertifikasi",
                        principalTable: "Lahan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Document_Petani_PetaniId",
                        column: x => x.PetaniId,
                        principalSchema: "esertifikasi",
                        principalTable: "Petani",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LandMappingRecord",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificationParticipantLahanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    GeoJson = table.Column<string>(type: "jsonb", nullable: true),
                    MappedArea = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MappedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    MappedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandMappingRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandMappingRecord_AspNetUsers_MappedByUserId",
                        column: x => x.MappedByUserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LandMappingRecord_CertificationParticipantLahan_Certificati~",
                        column: x => x.CertificationParticipantLahanId,
                        principalSchema: "esertifikasi",
                        principalTable: "CertificationParticipantLahan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PpeItemInspection",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PpeInspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    Condition = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PpeItemInspection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PpeItemInspection_PpeInspection_PpeInspectionId",
                        column: x => x.PpeInspectionId,
                        principalSchema: "esertifikasi",
                        principalTable: "PpeInspection",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WoodyPlantObservation",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WoodyPlantInspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TreeName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    HeightCentimeters = table.Column<decimal>(type: "numeric", nullable: true),
                    DamageSymptoms = table.Column<string>(type: "text", nullable: true),
                    Remarks = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WoodyPlantObservation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WoodyPlantObservation_WoodyPlantInspection_WoodyPlantInspec~",
                        column: x => x.WoodyPlantInspectionId,
                        principalSchema: "esertifikasi",
                        principalTable: "WoodyPlantInspection",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Certificate",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificationCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CertificationBody = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IssuedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Certificate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Certificate_AspNetUsers_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Certificate_CertificationCycle_CertificationCycleId",
                        column: x => x.CertificationCycleId,
                        principalSchema: "esertifikasi",
                        principalTable: "CertificationCycle",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Certificate_Document_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "esertifikasi",
                        principalTable: "Document",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentVersion",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileExtension = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentVersion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentVersion_AspNetUsers_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentVersion_Document_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "esertifikasi",
                        principalTable: "Document",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CertificateLahan",
                schema: "esertifikasi",
                columns: table => new
                {
                    CertificateId = table.Column<Guid>(type: "uuid", nullable: false),
                    LahanId = table.Column<Guid>(type: "uuid", nullable: false),
                    PetaniId = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalNumber = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LegalArea = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateLahan", x => new { x.CertificateId, x.LahanId });
                    table.ForeignKey(
                        name: "FK_CertificateLahan_Certificate_CertificateId",
                        column: x => x.CertificateId,
                        principalSchema: "esertifikasi",
                        principalTable: "Certificate",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CertificateParticipant",
                schema: "esertifikasi",
                columns: table => new
                {
                    CertificateId = table.Column<Guid>(type: "uuid", nullable: false),
                    PetaniId = table.Column<Guid>(type: "uuid", nullable: false),
                    PetaniName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PoktanId = table.Column<Guid>(type: "uuid", nullable: false),
                    PoktanName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateParticipant", x => new { x.CertificateId, x.PetaniId });
                    table.ForeignKey(
                        name: "FK_CertificateParticipant_Certificate_CertificateId",
                        column: x => x.CertificateId,
                        principalSchema: "esertifikasi",
                        principalTable: "Certificate",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssociationDocumentSubmission",
                schema: "esertifikasi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificationCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttachedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttachedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssociationDocumentSubmission", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssociationDocumentSubmission_AspNetUsers_AttachedByUserId",
                        column: x => x.AttachedByUserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssociationDocumentSubmission_AspNetUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssociationDocumentSubmission_AspNetUsers_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalSchema: "esertifikasi",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssociationDocumentSubmission_CertificationCycle_Certificat~",
                        column: x => x.CertificationCycleId,
                        principalSchema: "esertifikasi",
                        principalTable: "CertificationCycle",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssociationDocumentSubmission_DocumentType_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalSchema: "esertifikasi",
                        principalTable: "DocumentType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssociationDocumentSubmission_DocumentVersion_DocumentVersi~",
                        column: x => x.DocumentVersionId,
                        principalSchema: "esertifikasi",
                        principalTable: "DocumentVersion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssociationDocumentSubmission_Document_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "esertifikasi",
                        principalTable: "Document",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                schema: "esertifikasi",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "esertifikasi",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                schema: "esertifikasi",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                schema: "esertifikasi",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                schema: "esertifikasi",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "esertifikasi",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "esertifikasi",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Association_DesaId",
                schema: "esertifikasi",
                table: "Association",
                column: "DesaId");

            migrationBuilder.CreateIndex(
                name: "IX_Association_DistrictId",
                schema: "esertifikasi",
                table: "Association",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_Association_Nama",
                schema: "esertifikasi",
                table: "Association",
                column: "Nama",
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Association_ProvinceId",
                schema: "esertifikasi",
                table: "Association",
                column: "ProvinceId");

            migrationBuilder.CreateIndex(
                name: "IX_Association_RegencyId",
                schema: "esertifikasi",
                table: "Association",
                column: "RegencyId");

            migrationBuilder.CreateIndex(
                name: "IX_AssociationAdminAssignment_AssociationId",
                schema: "esertifikasi",
                table: "AssociationAdminAssignment",
                column: "AssociationId");

            migrationBuilder.CreateIndex(
                name: "IX_AssociationDocumentSubmission_AttachedByUserId",
                schema: "esertifikasi",
                table: "AssociationDocumentSubmission",
                column: "AttachedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AssociationDocumentSubmission_CertificationCycleId_Document~",
                schema: "esertifikasi",
                table: "AssociationDocumentSubmission",
                columns: new[] { "CertificationCycleId", "DocumentTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssociationDocumentSubmission_DocumentId",
                schema: "esertifikasi",
                table: "AssociationDocumentSubmission",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AssociationDocumentSubmission_DocumentTypeId",
                schema: "esertifikasi",
                table: "AssociationDocumentSubmission",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AssociationDocumentSubmission_DocumentVersionId",
                schema: "esertifikasi",
                table: "AssociationDocumentSubmission",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_AssociationDocumentSubmission_ReviewedByUserId",
                schema: "esertifikasi",
                table: "AssociationDocumentSubmission",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AssociationDocumentSubmission_SubmittedByUserId",
                schema: "esertifikasi",
                table: "AssociationDocumentSubmission",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditFinding_CertificationAuditId_Code",
                schema: "esertifikasi",
                table: "AuditFinding",
                columns: new[] { "CertificationAuditId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditFinding_ClosedByUserId",
                schema: "esertifikasi",
                table: "AuditFinding",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditFinding_PoktanId",
                schema: "esertifikasi",
                table: "AuditFinding",
                column: "PoktanId");

            migrationBuilder.CreateIndex(
                name: "IX_BaselineAssessment_AssessedByUserId",
                schema: "esertifikasi",
                table: "BaselineAssessment",
                column: "AssessedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BaselineAssessment_LahanId_Type",
                schema: "esertifikasi",
                table: "BaselineAssessment",
                columns: new[] { "LahanId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Certificate_CertificationCycleId",
                schema: "esertifikasi",
                table: "Certificate",
                column: "CertificationCycleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Certificate_DocumentId",
                schema: "esertifikasi",
                table: "Certificate",
                column: "DocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Certificate_Number",
                schema: "esertifikasi",
                table: "Certificate",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Certificate_UploadedByUserId",
                schema: "esertifikasi",
                table: "Certificate",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificationAudit_CertificationCycleId_Type",
                schema: "esertifikasi",
                table: "CertificationAudit",
                columns: new[] { "CertificationCycleId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CertificationAudit_CreatedByUserId",
                schema: "esertifikasi",
                table: "CertificationAudit",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificationCycle_AssociationId",
                schema: "esertifikasi",
                table: "CertificationCycle",
                column: "AssociationId",
                unique: true,
                filter: "\"IsCurrent\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_CertificationCycle_AssociationId_SequenceNumber",
                schema: "esertifikasi",
                table: "CertificationCycle",
                columns: new[] { "AssociationId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CertificationCycle_CreatedByUserId",
                schema: "esertifikasi",
                table: "CertificationCycle",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificationParticipant_CertificateEligibilityDecidedByUse~",
                schema: "esertifikasi",
                table: "CertificationParticipant",
                column: "CertificateEligibilityDecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificationParticipant_CertificationCycleId_PetaniId",
                schema: "esertifikasi",
                table: "CertificationParticipant",
                columns: new[] { "CertificationCycleId", "PetaniId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CertificationParticipant_PetaniId",
                schema: "esertifikasi",
                table: "CertificationParticipant",
                column: "PetaniId");

            migrationBuilder.CreateIndex(
                name: "IX_CertificationParticipantLahan_CertificationParticipantId_La~",
                schema: "esertifikasi",
                table: "CertificationParticipantLahan",
                columns: new[] { "CertificationParticipantId", "LahanId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CertificationParticipantLahan_LahanId",
                schema: "esertifikasi",
                table: "CertificationParticipantLahan",
                column: "LahanId");

            migrationBuilder.CreateIndex(
                name: "IX_ChemicalBufferInspection_LahanId",
                schema: "esertifikasi",
                table: "ChemicalBufferInspection",
                column: "LahanId");

            migrationBuilder.CreateIndex(
                name: "IX_ChemicalBufferInspection_MonitoringSubmissionId",
                schema: "esertifikasi",
                table: "ChemicalBufferInspection",
                column: "MonitoringSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_ChemicalBufferInspection_PetaniId",
                schema: "esertifikasi",
                table: "ChemicalBufferInspection",
                column: "PetaniId");

            migrationBuilder.CreateIndex(
                name: "IX_CycleDocumentRequirement_CertificationCycleId_DocumentTypeI~",
                schema: "esertifikasi",
                table: "CycleDocumentRequirement",
                columns: new[] { "CertificationCycleId", "DocumentTypeId", "OwnerType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CycleDocumentRequirement_DocumentTypeId",
                schema: "esertifikasi",
                table: "CycleDocumentRequirement",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CycleStepProgress_CertificationCycleId_Step",
                schema: "esertifikasi",
                table: "CycleStepProgress",
                columns: new[] { "CertificationCycleId", "Step" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Disclosure_CertificationCycleId_VersionNumber",
                schema: "esertifikasi",
                table: "Disclosure",
                columns: new[] { "CertificationCycleId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Disclosure_CreatedByUserId",
                schema: "esertifikasi",
                table: "Disclosure",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Disclosure_ReviewedByUserId",
                schema: "esertifikasi",
                table: "Disclosure",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DisclosureLahan_CertificationParticipantLahanId",
                schema: "esertifikasi",
                table: "DisclosureLahan",
                column: "CertificationParticipantLahanId");

            migrationBuilder.CreateIndex(
                name: "IX_DisclosureParticipant_CertificationParticipantId",
                schema: "esertifikasi",
                table: "DisclosureParticipant",
                column: "CertificationParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_District_Code",
                schema: "esertifikasi",
                table: "District",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_District_RegencyId_Name",
                schema: "esertifikasi",
                table: "District",
                columns: new[] { "RegencyId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Document_AssociationId_DocumentTypeId",
                schema: "esertifikasi",
                table: "Document",
                columns: new[] { "AssociationId", "DocumentTypeId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"AssociationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Document_CertificationCycleId_DocumentTypeId",
                schema: "esertifikasi",
                table: "Document",
                columns: new[] { "CertificationCycleId", "DocumentTypeId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"CertificationCycleId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Document_CertificationParticipantId_DocumentTypeId",
                schema: "esertifikasi",
                table: "Document",
                columns: new[] { "CertificationParticipantId", "DocumentTypeId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"CertificationParticipantId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Document_CertificationParticipantLahanId_DocumentTypeId",
                schema: "esertifikasi",
                table: "Document",
                columns: new[] { "CertificationParticipantLahanId", "DocumentTypeId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"CertificationParticipantLahanId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Document_DocumentTypeId",
                schema: "esertifikasi",
                table: "Document",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Document_LahanId_DocumentTypeId",
                schema: "esertifikasi",
                table: "Document",
                columns: new[] { "LahanId", "DocumentTypeId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"LahanId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Document_PetaniId_DocumentTypeId",
                schema: "esertifikasi",
                table: "Document",
                columns: new[] { "PetaniId", "DocumentTypeId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"PetaniId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Document_ReviewedByUserId",
                schema: "esertifikasi",
                table: "Document",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Document_Status_IsDeleted",
                schema: "esertifikasi",
                table: "Document",
                columns: new[] { "Status", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Document_UploadedByUserId",
                schema: "esertifikasi",
                table: "Document",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentType_OwnerType_Code",
                schema: "esertifikasi",
                table: "DocumentType",
                columns: new[] { "OwnerType", "Code" },
                unique: true,
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersion_DocumentId_VersionNumber",
                schema: "esertifikasi",
                table: "DocumentVersion",
                columns: new[] { "DocumentId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersion_UploadedByUserId",
                schema: "esertifikasi",
                table: "DocumentVersion",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FireIncident_LahanId",
                schema: "esertifikasi",
                table: "FireIncident",
                column: "LahanId");

            migrationBuilder.CreateIndex(
                name: "IX_FireIncident_MonitoringSubmissionId",
                schema: "esertifikasi",
                table: "FireIncident",
                column: "MonitoringSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_FireIncident_PetaniId",
                schema: "esertifikasi",
                table: "FireIncident",
                column: "PetaniId");

            migrationBuilder.CreateIndex(
                name: "IX_FirstAidKitInspection_MonitoringSubmissionId",
                schema: "esertifikasi",
                table: "FirstAidKitInspection",
                column: "MonitoringSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_FirstAidKitItemInspection_FirstAidKitInspectionId",
                schema: "esertifikasi",
                table: "FirstAidKitItemInspection",
                column: "FirstAidKitInspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_HcvLocationAssessment_LahanId",
                schema: "esertifikasi",
                table: "HcvLocationAssessment",
                column: "LahanId");

            migrationBuilder.CreateIndex(
                name: "IX_HcvLocationAssessment_MonitoringSubmissionId",
                schema: "esertifikasi",
                table: "HcvLocationAssessment",
                column: "MonitoringSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_HcvLocationAssessment_PetaniId",
                schema: "esertifikasi",
                table: "HcvLocationAssessment",
                column: "PetaniId");

            migrationBuilder.CreateIndex(
                name: "IX_IcsAuditorAssignment_PoktanId",
                schema: "esertifikasi",
                table: "IcsAuditorAssignment",
                column: "PoktanId");

            migrationBuilder.CreateIndex(
                name: "IX_Lahan_DesaId",
                schema: "esertifikasi",
                table: "Lahan",
                column: "DesaId");

            migrationBuilder.CreateIndex(
                name: "IX_Lahan_NoLegalitas",
                schema: "esertifikasi",
                table: "Lahan",
                column: "NoLegalitas");

            migrationBuilder.CreateIndex(
                name: "IX_Lahan_PetaniId_IsDeleted",
                schema: "esertifikasi",
                table: "Lahan",
                columns: new[] { "PetaniId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_LandBoundaryInspection_LahanId",
                schema: "esertifikasi",
                table: "LandBoundaryInspection",
                column: "LahanId");

            migrationBuilder.CreateIndex(
                name: "IX_LandBoundaryInspection_MonitoringSubmissionId",
                schema: "esertifikasi",
                table: "LandBoundaryInspection",
                column: "MonitoringSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_LandBoundaryInspection_PetaniId",
                schema: "esertifikasi",
                table: "LandBoundaryInspection",
                column: "PetaniId");

            migrationBuilder.CreateIndex(
                name: "IX_LandMappingRecord_CertificationParticipantLahanId",
                schema: "esertifikasi",
                table: "LandMappingRecord",
                column: "CertificationParticipantLahanId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LandMappingRecord_MappedByUserId",
                schema: "esertifikasi",
                table: "LandMappingRecord",
                column: "MappedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberComplaint_MonitoringSubmissionId",
                schema: "esertifikasi",
                table: "MemberComplaint",
                column: "MonitoringSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberComplaint_PetaniId",
                schema: "esertifikasi",
                table: "MemberComplaint",
                column: "PetaniId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberRegistration_AssociationId_Status",
                schema: "esertifikasi",
                table: "MemberRegistration",
                columns: new[] { "AssociationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberRegistration_ExistingPetaniId",
                schema: "esertifikasi",
                table: "MemberRegistration",
                column: "ExistingPetaniId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberRegistration_PoktanId_Status",
                schema: "esertifikasi",
                table: "MemberRegistration",
                columns: new[] { "PoktanId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberRegistration_Provider_ProviderSubjectId",
                schema: "esertifikasi",
                table: "MemberRegistration",
                columns: new[] { "Provider", "ProviderSubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberRegistration_ReviewedByUserId",
                schema: "esertifikasi",
                table: "MemberRegistration",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberRegistration_UserId",
                schema: "esertifikasi",
                table: "MemberRegistration",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringAttachment_MonitoringSubmissionId",
                schema: "esertifikasi",
                table: "MonitoringAttachment",
                column: "MonitoringSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringAttachment_UploadedByUserId",
                schema: "esertifikasi",
                table: "MonitoringAttachment",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringFollowUp_AssignedToUserId",
                schema: "esertifikasi",
                table: "MonitoringFollowUp",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringFollowUp_MonitoringSubmissionId",
                schema: "esertifikasi",
                table: "MonitoringFollowUp",
                column: "MonitoringSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringRecord_LahanId",
                schema: "esertifikasi",
                table: "MonitoringRecord",
                column: "LahanId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringRecord_PetaniId_LahanId_Category_MonitoringMonth",
                schema: "esertifikasi",
                table: "MonitoringRecord",
                columns: new[] { "PetaniId", "LahanId", "Category", "MonitoringMonth" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringRecord_ResponsibleUserId",
                schema: "esertifikasi",
                table: "MonitoringRecord",
                column: "ResponsibleUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringReferenceItem_Kind_Code",
                schema: "esertifikasi",
                table: "MonitoringReferenceItem",
                columns: new[] { "Kind", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringSubmission_AssociationId",
                schema: "esertifikasi",
                table: "MonitoringSubmission",
                column: "AssociationId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringSubmission_CreatedByUserId",
                schema: "esertifikasi",
                table: "MonitoringSubmission",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringSubmission_FinalizedByUserId",
                schema: "esertifikasi",
                table: "MonitoringSubmission",
                column: "FinalizedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringSubmission_PoktanId_Type_PeriodStart_PeriodEnd",
                schema: "esertifikasi",
                table: "MonitoringSubmission",
                columns: new[] { "PoktanId", "Type", "PeriodStart", "PeriodEnd" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringSubmission_ReopenedByUserId",
                schema: "esertifikasi",
                table: "MonitoringSubmission",
                column: "ReopenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantStepProgress_CertificationParticipantId_Step",
                schema: "esertifikasi",
                table: "ParticipantStepProgress",
                columns: new[] { "CertificationParticipantId", "Step" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantStepProgress_ResponsibleUserId",
                schema: "esertifikasi",
                table: "ParticipantStepProgress",
                column: "ResponsibleUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PestInspection_LahanId",
                schema: "esertifikasi",
                table: "PestInspection",
                column: "LahanId");

            migrationBuilder.CreateIndex(
                name: "IX_PestInspection_MonitoringSubmissionId",
                schema: "esertifikasi",
                table: "PestInspection",
                column: "MonitoringSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_PestInspection_PetaniId",
                schema: "esertifikasi",
                table: "PestInspection",
                column: "PetaniId");

            migrationBuilder.CreateIndex(
                name: "IX_Petani_ApplicationUserId",
                schema: "esertifikasi",
                table: "Petani",
                column: "ApplicationUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Petani_DesaId",
                schema: "esertifikasi",
                table: "Petani",
                column: "DesaId");

            migrationBuilder.CreateIndex(
                name: "IX_Petani_Nik",
                schema: "esertifikasi",
                table: "Petani",
                column: "Nik",
                unique: true,
                filter: "\"Nik\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Petani_PoktanId_IsDeleted",
                schema: "esertifikasi",
                table: "Petani",
                columns: new[] { "PoktanId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_PlantDiseaseInspection_LahanId",
                schema: "esertifikasi",
                table: "PlantDiseaseInspection",
                column: "LahanId");

            migrationBuilder.CreateIndex(
                name: "IX_PlantDiseaseInspection_MonitoringSubmissionId",
                schema: "esertifikasi",
                table: "PlantDiseaseInspection",
                column: "MonitoringSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlantDiseaseInspection_PetaniId",
                schema: "esertifikasi",
                table: "PlantDiseaseInspection",
                column: "PetaniId");

            migrationBuilder.CreateIndex(
                name: "IX_Poktan_AssociationId_Nama",
                schema: "esertifikasi",
                table: "Poktan",
                columns: new[] { "AssociationId", "Nama" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Poktan_ProvinceId",
                schema: "esertifikasi",
                table: "Poktan",
                column: "ProvinceId");

            migrationBuilder.CreateIndex(
                name: "IX_Poktan_RegencyId",
                schema: "esertifikasi",
                table: "Poktan",
                column: "RegencyId");

            migrationBuilder.CreateIndex(
                name: "IX_PoktanAdminAssignment_PoktanId",
                schema: "esertifikasi",
                table: "PoktanAdminAssignment",
                column: "PoktanId");

            migrationBuilder.CreateIndex(
                name: "IX_PpeInspection_LahanId",
                schema: "esertifikasi",
                table: "PpeInspection",
                column: "LahanId");

            migrationBuilder.CreateIndex(
                name: "IX_PpeInspection_MonitoringSubmissionId",
                schema: "esertifikasi",
                table: "PpeInspection",
                column: "MonitoringSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_PpeInspection_PetaniId",
                schema: "esertifikasi",
                table: "PpeInspection",
                column: "PetaniId");

            migrationBuilder.CreateIndex(
                name: "IX_PpeItemInspection_PpeInspectionId",
                schema: "esertifikasi",
                table: "PpeItemInspection",
                column: "PpeInspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProtectedSpeciesObservation_LahanId",
                schema: "esertifikasi",
                table: "ProtectedSpeciesObservation",
                column: "LahanId");

            migrationBuilder.CreateIndex(
                name: "IX_ProtectedSpeciesObservation_MonitoringSubmissionId",
                schema: "esertifikasi",
                table: "ProtectedSpeciesObservation",
                column: "MonitoringSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProtectedSpeciesObservation_PetaniId",
                schema: "esertifikasi",
                table: "ProtectedSpeciesObservation",
                column: "PetaniId");

            migrationBuilder.CreateIndex(
                name: "IX_Province_Code",
                schema: "esertifikasi",
                table: "Province",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Province_Name",
                schema: "esertifikasi",
                table: "Province",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_TokenHash",
                schema: "esertifikasi",
                table: "RefreshToken",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_UserId_ExpiresAt",
                schema: "esertifikasi",
                table: "RefreshToken",
                columns: new[] { "UserId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Regency_Code",
                schema: "esertifikasi",
                table: "Regency",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Regency_ProvinceId_Name",
                schema: "esertifikasi",
                table: "Regency",
                columns: new[] { "ProvinceId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_RegionDatasetImport_DatasetSha256",
                schema: "esertifikasi",
                table: "RegionDatasetImport",
                column: "DatasetSha256");

            migrationBuilder.CreateIndex(
                name: "IX_RegionDatasetImport_Version",
                schema: "esertifikasi",
                table: "RegionDatasetImport",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainingAttendance_PetaniId",
                schema: "esertifikasi",
                table: "TrainingAttendance",
                column: "PetaniId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSession_CertificationCycleId",
                schema: "esertifikasi",
                table: "TrainingSession",
                column: "CertificationCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSession_CreatedByUserId",
                schema: "esertifikasi",
                table: "TrainingSession",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TurneraInspection_LahanId",
                schema: "esertifikasi",
                table: "TurneraInspection",
                column: "LahanId");

            migrationBuilder.CreateIndex(
                name: "IX_TurneraInspection_MonitoringSubmissionId",
                schema: "esertifikasi",
                table: "TurneraInspection",
                column: "MonitoringSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_TurneraInspection_PetaniId",
                schema: "esertifikasi",
                table: "TurneraInspection",
                column: "PetaniId");

            migrationBuilder.CreateIndex(
                name: "IX_Village_Code",
                schema: "esertifikasi",
                table: "Village",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Village_DistrictId_Name",
                schema: "esertifikasi",
                table: "Village",
                columns: new[] { "DistrictId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_WeedInspection_LahanId",
                schema: "esertifikasi",
                table: "WeedInspection",
                column: "LahanId");

            migrationBuilder.CreateIndex(
                name: "IX_WeedInspection_MonitoringSubmissionId",
                schema: "esertifikasi",
                table: "WeedInspection",
                column: "MonitoringSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_WeedInspection_PetaniId",
                schema: "esertifikasi",
                table: "WeedInspection",
                column: "PetaniId");

            migrationBuilder.CreateIndex(
                name: "IX_WoodyPlantInspection_LahanId",
                schema: "esertifikasi",
                table: "WoodyPlantInspection",
                column: "LahanId");

            migrationBuilder.CreateIndex(
                name: "IX_WoodyPlantInspection_MonitoringSubmissionId",
                schema: "esertifikasi",
                table: "WoodyPlantInspection",
                column: "MonitoringSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_WoodyPlantInspection_PetaniId",
                schema: "esertifikasi",
                table: "WoodyPlantInspection",
                column: "PetaniId");

            migrationBuilder.CreateIndex(
                name: "IX_WoodyPlantObservation_WoodyPlantInspectionId",
                schema: "esertifikasi",
                table: "WoodyPlantObservation",
                column: "WoodyPlantInspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransition_CertificationCycleId_ChangedAt",
                schema: "esertifikasi",
                table: "WorkflowTransition",
                columns: new[] { "CertificationCycleId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransition_UserId",
                schema: "esertifikasi",
                table: "WorkflowTransition",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkplaceAccidentIncident_LahanId",
                schema: "esertifikasi",
                table: "WorkplaceAccidentIncident",
                column: "LahanId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkplaceAccidentIncident_MonitoringSubmissionId",
                schema: "esertifikasi",
                table: "WorkplaceAccidentIncident",
                column: "MonitoringSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkplaceAccidentIncident_PetaniId",
                schema: "esertifikasi",
                table: "WorkplaceAccidentIncident",
                column: "PetaniId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "AssociationAdminAssignment",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "AssociationDocumentSubmission",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "AuditFinding",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "BaselineAssessment",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "CertificateLahan",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "CertificateParticipant",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "ChemicalBufferInspection",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "CycleDocumentRequirement",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "CycleStepProgress",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "DisclosureLahan",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "DisclosureParticipant",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "FireIncident",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "FirstAidKitItemInspection",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "HcvLocationAssessment",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "IcsAuditorAssignment",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "LandBoundaryInspection",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "LandMappingRecord",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "MemberComplaint",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "MemberRegistration",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "MonitoringAttachment",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "MonitoringDefinition",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "MonitoringFollowUp",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "MonitoringRecord",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "MonitoringReferenceItem",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "ParticipantStepProgress",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "PestInspection",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "PlantDiseaseInspection",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "PoktanAdminAssignment",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "PpeItemInspection",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "ProtectedSpeciesObservation",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "RefreshToken",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "RegionDatasetImport",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "TrainingAttendance",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "TurneraInspection",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "WeedInspection",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "WoodyPlantObservation",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "WorkflowTransition",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "WorkplaceAccidentIncident",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "AspNetRoles",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "DocumentVersion",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "CertificationAudit",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "Certificate",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "ChemicalBufferMonitoring",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "Disclosure",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "FireMonitoring",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "FirstAidKitInspection",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "LandBoundaryMonitoring",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "MemberComplaintMonitoring",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "PestMonitoring",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "PlantDiseaseMonitoring",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "PpeInspection",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "HighConservationValueMonitoring",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "TrainingSession",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "TurneraMonitoring",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "WeedMonitoring",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "WoodyPlantInspection",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "WorkplaceAccidentMonitoring",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "Document",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "FirstAidKitMonitoring",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "PpeMonitoring",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "WoodyPlantMonitoring",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "CertificationParticipantLahan",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "DocumentType",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "MonitoringSubmission",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "CertificationParticipant",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "Lahan",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "CertificationCycle",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "Petani",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "AspNetUsers",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "Poktan",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "Association",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "Village",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "District",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "Regency",
                schema: "esertifikasi");

            migrationBuilder.DropTable(
                name: "Province",
                schema: "esertifikasi");
        }
    }
}
