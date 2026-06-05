# ═══════════════════════════════════════════════════════════════
# 🏗️ منصة نور — Dockerfile (Multi-Stage Production Build)
# ═══════════════════════════════════════════════════════════════

# ─── المرحلة 1: البناء (Build Stage) ───────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# نسخ ملفات المشاريع أولاً لتفعيل Docker Layer Caching على الـ restore
COPY NoorPlatform.Core/NoorPlatform.Core.csproj          NoorPlatform.Core/
COPY NoorPlatform.Infrastructure/NoorPlatform.Infrastructure.csproj  NoorPlatform.Infrastructure/
COPY NoorPlatform.Api/NoorPlatform.Api.csproj             NoorPlatform.Api/

# استعادة الحزم (مخزنة مؤقتاً ما لم تتغير ملفات csproj)
RUN dotnet restore NoorPlatform.Api/NoorPlatform.Api.csproj

# نسخ كامل الكود المصدري
COPY NoorPlatform.Core/          NoorPlatform.Core/
COPY NoorPlatform.Infrastructure/ NoorPlatform.Infrastructure/
COPY NoorPlatform.Api/            NoorPlatform.Api/

# بناء ونشر بوضع الإنتاج
RUN dotnet publish NoorPlatform.Api/NoorPlatform.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ─── المرحلة 2: التشغيل (Runtime Stage) ───────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# أمان: تشغيل التطبيق بمستخدم غير root
RUN groupadd -r noor && useradd -r -g noor -s /sbin/nologin noor

# نسخ مخرجات البناء فقط (بدون SDK أو كود مصدري)
COPY --from=build /app/publish .

# إنشاء مجلد المكتبة وضبط الصلاحيات
RUN mkdir -p /app/wwwroot/library && chown -R noor:noor /app

# ─── ضبط بيئة التشغيل ─────────────────────────────────────────
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
# تحسين أداء GC داخل الحاوية
ENV DOTNET_gcServer=1
ENV DOTNET_GCHeapHardLimit=0x10000000

EXPOSE 8080

# التبديل للمستخدم الآمن
USER noor

# فحص صحة التطبيق (Health Check)
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -sf http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "NoorPlatform.Api.dll"]
