using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoorPlatform.Infrastructure.Migrations
{
    /// <summary>
    /// مزامنة أدوار Identity للمستخدمين الحاليين.
    /// بعد إصلاح AccountProvisioningService لاستدعاء AddToRoleAsync،
    /// نحتاج لتحديث المستخدمين المُنشأين سابقاً بدون أدوار Identity.
    /// </summary>
    public partial class SyncExistingUsersToIdentityRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // إضافة أدوار Identity المفقودة لجميع المستخدمين الحاليين
            // Role Enum: 0 = Admin, 1 = Teacher, 2 = Student, 3 = Parent
            migrationBuilder.Sql(@"
                INSERT INTO AspNetUserRoles (UserId, RoleId)
                SELECT u.Id, r.Id
                FROM AspNetUsers u
                INNER JOIN AspNetRoles r
                    ON r.Name = CASE u.Role
                        WHEN 0 THEN 'Admin'
                        WHEN 1 THEN 'Teacher'
                        WHEN 2 THEN 'Student'
                        WHEN 3 THEN 'Parent'
                    END
                WHERE NOT EXISTS (
                    SELECT 1 FROM AspNetUserRoles ur
                    WHERE ur.UserId = u.Id AND ur.RoleId = r.Id
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // تراجع: إزالة جميع أدوار المستخدمين المُضافة بواسطة هذه الـ Migration
            // ملاحظة: هذا يزيل فقط الأدوار التي تتطابق مع Enum الحالي
            migrationBuilder.Sql(@"
                DELETE ur FROM AspNetUserRoles ur
                INNER JOIN AspNetUsers u ON ur.UserId = u.Id
                INNER JOIN AspNetRoles r ON ur.RoleId = r.Id
                WHERE r.Name = CASE u.Role
                    WHEN 0 THEN 'Admin'
                    WHEN 1 THEN 'Teacher'
                    WHEN 2 THEN 'Student'
                    WHEN 3 THEN 'Parent'
                END;
            ");
        }
    }
}
