<div dir="rtl">

# منصة نور (Noor Platform) 🌙

**منصة متكاملة لإدارة حلقات تحفيظ القرآن الكريم والمراكز الإسلامية**

تهدف منصة نور إلى رقمنة وأتمتة المهام اليومية لإدارة المراكز القرآنية، بدءاً من تسجيل الطلاب، تتبع الحفظ والمراجعة، إدارة الحضور والانصراف، إصدار الشهادات، وتوفير لوحات تحكم مخصصة لكل من: الإدارة، المعلمين، أولياء الأمور، والطلاب. تم بناء المنصة باستخدام أحدث تقنيات الـ .NET لتكون مستقرة، سريعة، ومحمية بالكامل للاستخدام المؤسسي (Production-Ready).

</div>

---

## 🏗️ Overview & Architecture
Noor Platform is designed using a **Clean Architecture** approach, ensuring separation of concerns, high maintainability, and scalability. The system is divided into logical layers:
- **NoorPlatform.Api:** The presentation layer exposing RESTful APIs and serving the robust HTML/JS frontend.
- **NoorPlatform.Core:** Contains the domain entities (Student, Teacher, HifzRecord, AuditLog) and enums.
- **NoorPlatform.Infrastructure:** Handles data persistence via Entity Framework Core (EF Core), Identity configuration, and automated database interceptors.

The system utilizes **JWT (JSON Web Tokens)** for stateless authentication and features a multi-role hierarchy (`Admin`, `Teacher`, `Student`, `Parent`) with fine-grained endpoint authorization.

---

## 🛡️ Security Features (Hardened for Production)
Security was a top priority during development. The following safeguards have been rigorously implemented:
- **SQL Injection Prevention:** Full reliance on EF Core LINQ and parameterized raw SQL queries.
- **XSS (Cross-Site Scripting) Protection:** 
  - Dynamic UI components use `textContent` instead of `innerHTML`.
  - Backend HTML encoders sanitizing sensitive outputs (e.g., Certificate Generation).
  - Implementation of a strict **Content Security Policy (CSP)**.
- **IDOR (Insecure Direct Object Reference) Prevention:** Every endpoint interacting with user data strictly verifies ownership using JWT Claims (e.g., a parent can only view their own children's records).
- **Directory Traversal Mitigation:** Uploaded files (Library Items) are tightly controlled. Direct file access is blocked via the `BlockLibraryUploadsMiddleware` (returning `403 Forbidden`).
- **Automated Audit Trail (Black Box):** An EF Core Interceptor automatically records every `Create`, `Update`, and `Delete` action to an `AuditLogs` table with old/new values, strictly filtering out sensitive fields (e.g., passwords).
- **Network Security:** HSTS enabled, strict CORS policies for production domains, and global error handling ensuring no sensitive stack traces are leaked.

---

## ✨ Features List
- **Advanced Role-Based Dashboards:** Unique UI/UX and API scoping tailored for Admins, Teachers, Parents, and Students.
- **Quranic Progress Tracker:** Gamified Hifz tracking with automated badges, progress bars, and streak counters.
- **Financial Management:** Tuition tracking, payment statuses, and invoice generation.
- **Automated Certifications:** Dynamically generated, printable HTML-to-PDF completion certificates.
- **Library System:** Centralized document and media sharing for circles with strict access control.
- **Real-Time Data Rendering:** UI Debouncing, DOM caching, and efficient DOM-manipulation to ensure a highly responsive user experience.
- **Soft Deletion:** Archiving entities rather than physical deletion to preserve historical integrity.
- **Network Resilience:** Frontend `offline/online` awareness alerting users when the connection drops.

---

## 💻 Technology Stack
- **Backend Framework:** .NET 9.0 (ASP.NET Core Web API)
- **ORM:** Entity Framework Core 9.0
- **Database:** Microsoft SQL Server 2022
- **Frontend:** Vanilla HTML5, CSS3, ES6 JavaScript (No bloated frameworks, 100% bespoke)
- **Authentication:** ASP.NET Core Identity & JWT Bearer Tokens
- **Containerization:** Docker & Docker Compose (Multi-stage builds)

---

## 🚀 Deployment Guide (Docker)

The project is fully containerized and optimized for production.

### Prerequisites
- Docker Engine & Docker Compose installed on your host.

### 1. Configure the Environment
Create a `.env` file in the root directory (where `docker-compose.yml` is located). Fill it with secure values:
```env
DB_PASSWORD=Your_Strong_Db_Password_Here!2027
JWT_SECRET_KEY=Your_Very_Long_And_Secure_JWT_Secret_Key_For_Noor_Platform_2027
JWT_ISSUER=NoorPlatform
JWT_AUDIENCE=NoorPlatformClients
```

### 2. Build and Run
Execute the following command to start the SQL Server and the Noor API in detached mode:
```bash
docker-compose up -d --build
```
*Note: The API container relies on a `healthcheck` and will wait for the database to be fully operational before booting up.*

### 3. Verify
Check the container logs to ensure successful startup:
```bash
docker-compose logs -f api
```
Access the application at: `http://localhost:8080` (or configure a Reverse Proxy like Nginx/Traefik to bind it to your domain).

---

> Built with passion and commitment for the graduation defense of January 2027.
