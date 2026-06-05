using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoorPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAuditLogSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ─── حذف الجدول القديم وإعادة إنشائه بالمخطط الجديد ───
            // لأن SQL Server لا يسمح بتغيير عمود IDENTITY إلى uniqueidentifier مباشرة
            migrationBuilder.Sql(@"
                DROP TABLE IF EXISTS [AuditLogs];

                CREATE TABLE [AuditLogs] (
                    [Id]         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
                    [UserId]     NVARCHAR(450)    NOT NULL DEFAULT '',
                    [Action]     NVARCHAR(50)     NOT NULL DEFAULT '',
                    [EntityName] NVARCHAR(100)    NOT NULL DEFAULT '',
                    [EntityId]   NVARCHAR(100)    NULL,
                    [OldValues]  NVARCHAR(MAX)    NULL,
                    [NewValues]  NVARCHAR(MAX)    NULL,
                    [Timestamp]  DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
                );

                CREATE NONCLUSTERED INDEX [IX_AuditLogs_Timestamp]
                    ON [AuditLogs] ([Timestamp] DESC);

                CREATE NONCLUSTERED INDEX [IX_AuditLogs_EntityName_EntityId]
                    ON [AuditLogs] ([EntityName], [EntityId]);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ─── إعادة الجدول للمخطط القديم (Rollback) ───
            migrationBuilder.Sql(@"
                DROP TABLE IF EXISTS [AuditLogs];

                CREATE TABLE [AuditLogs] (
                    [Id]         INT              NOT NULL IDENTITY(1, 1),
                    [UserId]     NVARCHAR(MAX)    NOT NULL DEFAULT '',
                    [Action]     NVARCHAR(50)     NOT NULL DEFAULT '',
                    [EntityType] NVARCHAR(100)    NOT NULL DEFAULT '',
                    [EntityId]   NVARCHAR(100)    NULL,
                    [OldValues]  NVARCHAR(MAX)    NULL,
                    [NewValues]  NVARCHAR(MAX)    NULL,
                    [Timestamp]  DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
                    [IpAddress]  NVARCHAR(50)     NULL,
                    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
                );
            ");
        }
    }
}
