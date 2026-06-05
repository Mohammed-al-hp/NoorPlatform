using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NoorPlatform.Core.Entities;

namespace NoorPlatform.Infrastructure.Data.Interceptors
{
    public class AuditInterceptor : SaveChangesInterceptor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditInterceptor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            ProcessAuditLogs(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            ProcessAuditLogs(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void ProcessAuditLogs(DbContext? context)
        {
            if (context == null) return;

            var httpContext = _httpContextAccessor.HttpContext;
            var userId = httpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "System";

            var ignoredProperties = new[] { "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "ResetToken" };

            var auditEntries = new List<AuditEntry>();

            foreach (var entry in context.ChangeTracker.Entries())
            {
                if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    continue;

                var auditEntry = new AuditEntry(entry)
                {
                    UserId = userId,
                    EntityName = entry.Entity.GetType().Name,
                    Timestamp = DateTime.UtcNow
                };

                auditEntries.Add(auditEntry);

                foreach (var property in entry.Properties)
                {
                    if (property.IsTemporary)
                    {
                        auditEntry.TemporaryProperties.Add(property);
                        continue;
                    }

                    string propertyName = property.Metadata.Name;
                    
                    if (property.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[propertyName] = property.CurrentValue;
                        continue;
                    }

                    if (ignoredProperties.Contains(propertyName))
                        continue;

                    switch (entry.State)
                    {
                        case EntityState.Added:
                            auditEntry.NewValues[propertyName] = property.CurrentValue;
                            auditEntry.Action = AuditAction.Create;
                            break;

                        case EntityState.Deleted:
                            auditEntry.OldValues[propertyName] = property.OriginalValue;
                            auditEntry.Action = AuditAction.Delete;
                            break;

                        case EntityState.Modified:
                            if (property.IsModified)
                            {
                                auditEntry.Action = AuditAction.Update;
                                auditEntry.OldValues[propertyName] = property.OriginalValue;
                                auditEntry.NewValues[propertyName] = property.CurrentValue;
                            }
                            break;
                    }
                }
            }

            foreach (var auditEntry in auditEntries)
            {
                context.Set<AuditLog>().Add(auditEntry.ToAuditLog());
            }
        }
    }

    public class AuditEntry
    {
        public AuditEntry(EntityEntry entry)
        {
            Entry = entry;
        }

        public EntityEntry Entry { get; }
        public string UserId { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public AuditAction Action { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object?> KeyValues { get; } = new();
        public Dictionary<string, object?> OldValues { get; } = new();
        public Dictionary<string, object?> NewValues { get; } = new();
        public List<PropertyEntry> TemporaryProperties { get; } = new();

        public AuditLog ToAuditLog()
        {
            return new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                Action = Action,
                EntityName = EntityName,
                EntityId = KeyValues.Count > 0 ? KeyValues.Values.First()?.ToString() ?? string.Empty : string.Empty,
                OldValues = OldValues.Count == 0 ? null : JsonSerializer.Serialize(OldValues),
                NewValues = NewValues.Count == 0 ? null : JsonSerializer.Serialize(NewValues),
                Timestamp = Timestamp
            };
        }
    }
}
