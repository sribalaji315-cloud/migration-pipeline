using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Haworth.MdPipeline.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialMetadataSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: true),
                    SecurityStamp = table.Column<string>(type: "TEXT", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", nullable: false),
                    EntityId = table.Column<string>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ChangedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BronzeVersions",
                columns: table => new
                {
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    SyncedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MaxUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Mode = table.Column<string>(type: "TEXT", nullable: false),
                    ChangedItems = table.Column<int>(type: "INTEGER", nullable: false),
                    MappingRows = table.Column<long>(type: "INTEGER", nullable: false),
                    MtItems = table.Column<int>(type: "INTEGER", nullable: false),
                    SwingItems = table.Column<int>(type: "INTEGER", nullable: false),
                    SwingExtractedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SwingItemHash = table.Column<string>(type: "TEXT", nullable: true),
                    SwingBomRows = table.Column<long>(type: "INTEGER", nullable: false),
                    SwingBomExtractedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SwingBomHash = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedByRunId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BronzeVersions", x => x.Version);
                });

            migrationBuilder.CreateTable(
                name: "ReferenceVersions",
                columns: table => new
                {
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UploadedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    FilesJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceVersions", x => x.Version);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderKey = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExportedItems",
                columns: table => new
                {
                    ItemId = table.Column<string>(type: "TEXT", nullable: false),
                    WaveId = table.Column<string>(type: "TEXT", nullable: false),
                    RunId = table.Column<string>(type: "TEXT", nullable: false),
                    ExportedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsReexport = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportedItems", x => new { x.ItemId, x.WaveId });
                });

            migrationBuilder.CreateTable(
                name: "IssueGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GroupKey = table.Column<string>(type: "TEXT", nullable: false),
                    IssueType = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", nullable: false),
                    FixSource = table.Column<string>(type: "TEXT", nullable: false),
                    FixTarget = table.Column<string>(type: "TEXT", nullable: false),
                    FixAction = table.Column<string>(type: "TEXT", nullable: false),
                    AffectedItems = table.Column<int>(type: "INTEGER", nullable: false),
                    AffectedBoms = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstSeenRunId = table.Column<string>(type: "TEXT", nullable: false),
                    LastSeenRunId = table.Column<string>(type: "TEXT", nullable: false),
                    ResolvedInRunId = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    AcknowledgedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PipelineRuns",
                columns: table => new
                {
                    RunId = table.Column<string>(type: "TEXT", nullable: false),
                    RunType = table.Column<string>(type: "TEXT", nullable: false),
                    SyncRequested = table.Column<bool>(type: "INTEGER", nullable: false),
                    SyncMode = table.Column<string>(type: "TEXT", nullable: true),
                    BronzeVersion = table.Column<int>(type: "INTEGER", nullable: true),
                    ReferenceVersion = table.Column<int>(type: "INTEGER", nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    BomTypePolicyJson = table.Column<string>(type: "TEXT", nullable: false),
                    SourceAnalysisRunId = table.Column<string>(type: "TEXT", nullable: true),
                    WaveId = table.Column<string>(type: "TEXT", nullable: true),
                    SilverPath = table.Column<string>(type: "TEXT", nullable: true),
                    GoldPath = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    StartedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    QueuedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineRuns", x => x.RunId);
                    table.ForeignKey(
                        name: "FK_PipelineRuns_BronzeVersions_BronzeVersion",
                        column: x => x.BronzeVersion,
                        principalTable: "BronzeVersions",
                        principalColumn: "Version",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PipelineRuns_PipelineRuns_SourceAnalysisRunId",
                        column: x => x.SourceAnalysisRunId,
                        principalTable: "PipelineRuns",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PipelineRuns_ReferenceVersions_ReferenceVersion",
                        column: x => x.ReferenceVersion,
                        principalTable: "ReferenceVersions",
                        principalColumn: "Version",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PipelineRunSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<string>(type: "TEXT", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    Step = table.Column<string>(type: "TEXT", nullable: false),
                    OutputTables = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    RowsOut = table.Column<long>(type: "INTEGER", nullable: false),
                    DurationSeconds = table.Column<double>(type: "REAL", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineRunSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PipelineRunSteps_PipelineRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "PipelineRuns",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TypeDecisions",
                columns: table => new
                {
                    ItemId = table.Column<string>(type: "TEXT", nullable: false),
                    Decision = table.Column<string>(type: "TEXT", nullable: false),
                    InheritedAttrsAtDecision = table.Column<string>(type: "TEXT", nullable: false),
                    SwingTypeCodeAtDecision = table.Column<string>(type: "TEXT", nullable: true),
                    LastSeenRunId = table.Column<string>(type: "TEXT", nullable: false),
                    DecidedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TypeDecisions", x => x.ItemId);
                    table.ForeignKey(
                        name: "FK_TypeDecisions_PipelineRuns_LastSeenRunId",
                        column: x => x.LastSeenRunId,
                        principalTable: "PipelineRuns",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Waves",
                columns: table => new
                {
                    WaveId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    TopBomsJson = table.Column<string>(type: "TEXT", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    BomTypePolicyJson = table.Column<string>(type: "TEXT", nullable: false),
                    Items = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    BronzeVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ReferenceVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastExportRunId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Waves", x => x.WaveId);
                    table.ForeignKey(
                        name: "FK_Waves_BronzeVersions_BronzeVersion",
                        column: x => x.BronzeVersion,
                        principalTable: "BronzeVersions",
                        principalColumn: "Version",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Waves_PipelineRuns_LastExportRunId",
                        column: x => x.LastExportRunId,
                        principalTable: "PipelineRuns",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Waves_ReferenceVersions_ReferenceVersion",
                        column: x => x.ReferenceVersion,
                        principalTable: "ReferenceVersions",
                        principalColumn: "Version",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogEntries_EntityType_EntityId",
                table: "AuditLogEntries",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExportedItems_RunId",
                table: "ExportedItems",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_ExportedItems_WaveId",
                table: "ExportedItems",
                column: "WaveId");

            migrationBuilder.CreateIndex(
                name: "IX_IssueGroups_FirstSeenRunId",
                table: "IssueGroups",
                column: "FirstSeenRunId");

            migrationBuilder.CreateIndex(
                name: "IX_IssueGroups_GroupKey_IssueType",
                table: "IssueGroups",
                columns: new[] { "GroupKey", "IssueType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IssueGroups_LastSeenRunId",
                table: "IssueGroups",
                column: "LastSeenRunId");

            migrationBuilder.CreateIndex(
                name: "IX_IssueGroups_ResolvedInRunId",
                table: "IssueGroups",
                column: "ResolvedInRunId");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRuns_BronzeVersion",
                table: "PipelineRuns",
                column: "BronzeVersion");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRuns_QueuedAt",
                table: "PipelineRuns",
                column: "QueuedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRuns_ReferenceVersion",
                table: "PipelineRuns",
                column: "ReferenceVersion");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRuns_SourceAnalysisRunId",
                table: "PipelineRuns",
                column: "SourceAnalysisRunId");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRuns_Status",
                table: "PipelineRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRuns_WaveId",
                table: "PipelineRuns",
                column: "WaveId");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRunSteps_RunId_Sequence",
                table: "PipelineRunSteps",
                columns: new[] { "RunId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TypeDecisions_LastSeenRunId",
                table: "TypeDecisions",
                column: "LastSeenRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Waves_BronzeVersion",
                table: "Waves",
                column: "BronzeVersion");

            migrationBuilder.CreateIndex(
                name: "IX_Waves_LastExportRunId",
                table: "Waves",
                column: "LastExportRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Waves_ReferenceVersion",
                table: "Waves",
                column: "ReferenceVersion");

            migrationBuilder.AddForeignKey(
                name: "FK_ExportedItems_PipelineRuns_RunId",
                table: "ExportedItems",
                column: "RunId",
                principalTable: "PipelineRuns",
                principalColumn: "RunId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExportedItems_Waves_WaveId",
                table: "ExportedItems",
                column: "WaveId",
                principalTable: "Waves",
                principalColumn: "WaveId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IssueGroups_PipelineRuns_FirstSeenRunId",
                table: "IssueGroups",
                column: "FirstSeenRunId",
                principalTable: "PipelineRuns",
                principalColumn: "RunId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IssueGroups_PipelineRuns_LastSeenRunId",
                table: "IssueGroups",
                column: "LastSeenRunId",
                principalTable: "PipelineRuns",
                principalColumn: "RunId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IssueGroups_PipelineRuns_ResolvedInRunId",
                table: "IssueGroups",
                column: "ResolvedInRunId",
                principalTable: "PipelineRuns",
                principalColumn: "RunId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PipelineRuns_Waves_WaveId",
                table: "PipelineRuns",
                column: "WaveId",
                principalTable: "Waves",
                principalColumn: "WaveId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Waves_PipelineRuns_LastExportRunId",
                table: "Waves");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "AuditLogEntries");

            migrationBuilder.DropTable(
                name: "ExportedItems");

            migrationBuilder.DropTable(
                name: "IssueGroups");

            migrationBuilder.DropTable(
                name: "PipelineRunSteps");

            migrationBuilder.DropTable(
                name: "TypeDecisions");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "PipelineRuns");

            migrationBuilder.DropTable(
                name: "Waves");

            migrationBuilder.DropTable(
                name: "BronzeVersions");

            migrationBuilder.DropTable(
                name: "ReferenceVersions");
        }
    }
}
