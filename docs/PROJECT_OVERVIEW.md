# ExamFlow — Project Overview

ExamFlow PDF fayllarından test suallarını avtomatik çıxaran və həmin suallar əsasında təsadüfi sınaq imtahanları keçirən veb-platformadır.

## Əsas axın

1. İstifadəçi qeydiyyatdan keçir / daxil olur.
2. PDF yükləyir → sistem arxa planda sualları, variantları və (qalın/rəngli şriftdən) düzgün cavabı çıxarır.
3. Yükləyən şəxs (və ya Admin) sualları yoxlayır, redaktə edir, düzgün cavabı təsdiqləyir.
4. İstənilən istifadəçi bir və ya bir neçə PDF seçib 30 və ya 50 suallıq imtahan başladır.
5. İmtahan vaxt limiti ilə keçirilir, cavablar serverdə saxlanılır.
6. Backend nəticəni hesablayır, səhvləri və düzgün cavabları göstərir.

## Təsdiqlənmiş qərarlar (istifadəçi cavabları)

| Mövzu | Qərar |
|---|---|
| İstifadəçilər | Çox istifadəçi, **ortaq sual bankı**, imtahan tarixçəsi şəxsi |
| Redaktə hüququ | Sualı **yalnız PDF-i yükləyən** və **Admin** redaktə/silə bilər |
| Düzgün cavab PDF-də | **Vizual** — qalın (bold) və ya rəngli şrift |
| PDF dili | Azərbaycan dili |
| İmtahan mənbəyi | İstifadəçinin **seçdiyi PDF-lər** |
| Unverified suallar | İmtahana düşür, **ballanmır** (faizə təsir etmir) |
| Vaxt limiti | Rejimə görə sabit (konfiqurasiya edilə bilən) |
| Deploy | Əvvəlcə lokal (lokal SQL Server instance), sonra hosting (SQL Server / Azure SQL) |
| Database | **Microsoft SQL Server** (PostgreSQL əvəzinə, sonradan dəyişdirilib) |
| Runtime | **.NET 9** (mövcud SDK; .NET 10 LTS-ə keçid planlaşdırılır) |

## Texnologiya xülasəsi

- **Backend:** .NET 9, ASP.NET Core Minimal APIs, EF Core 9 (`Microsoft.EntityFrameworkCore.SqlServer`), ASP.NET Core Identity
- **PDF:** PdfPig
- **DB:** Microsoft SQL Server 2022 (dev: lokal instance, Windows Authentication)
- **Frontend:** React 19, TypeScript, Vite, Tailwind CSS v4, shadcn/ui, TanStack Query, React Router, React Hook Form + Zod

Əsaslandırma: [ARCHITECTURE.md](ARCHITECTURE.md).

## MVP-dən kənar (gələcək)

- Skan edilmiş PDF-lər üçün OCR
- Statistik analitika (mövzu üzrə zəif tərəflər, sual çətinlik dərəcəsi)
- Fayl saxlanmasının cloud blob-a köçürülməsi
- Çoxdilli interfeys

## Sənədlər xəritəsi

| Sənəd | Məzmun |
|---|---|
| [REQUIREMENTS.md](REQUIREMENTS.md) | Funksional və qeyri-funksional tələblər |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Arxitektura seçimi, stack, solution strukturu, təhlükəsizlik |
| [DATABASE_SCHEMA.md](DATABASE_SCHEMA.md) | Cədvəllər, əlaqələr, indekslər, migration strategiyası |
| [API_DESIGN.md](API_DESIGN.md) | REST endpoint-lər, DTO-lar, xəta formatı |
| [PDF_PROCESSING.md](PDF_PROCESSING.md) | Parsing pipeline, düzgün cavab aşkarlanması |
| [EXAM_ENGINE.md](EXAM_ENGINE.md) | Random seçim, sessiya, taymer, qiymətləndirmə |
| [FRONTEND_DESIGN.md](FRONTEND_DESIGN.md) | Səhifələr, komponentlər, UI qaydaları |
| [IMPLEMENTATION_ROADMAP.md](IMPLEMENTATION_ROADMAP.md) | Mərhələlər və acceptance criteria |
