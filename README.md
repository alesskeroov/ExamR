# ExamR

**Turn exam PDFs and Word documents into timed practice exams, study sessions and live quiz battles.**

![.NET 9](https://img.shields.io/badge/.NET-9-512BD4?logo=dotnet&logoColor=white)
![React 19](https://img.shields.io/badge/React-19-61DAFB?logo=react&logoColor=black)
![TypeScript](https://img.shields.io/badge/TypeScript-5-3178C6?logo=typescript&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL%20Server-EF%20Core%209-CC2927?logo=microsoftsqlserver&logoColor=white)
![Tailwind CSS](https://img.shields.io/badge/Tailwind-v4-06B6D4?logo=tailwindcss&logoColor=white)

ExamR parses questions and answers from uploaded **PDF** and **DOCX** files, stores them as a question bank, and lets users practise in several ways.

---

## Features

| Area | What it does |
|---|---|
| **Document parsing** | Upload PDF/DOCX files. Questions, options and correct answers are extracted automatically, with a processing report. |
| **Custom exams** | Random exams from a chosen question range, with a timer, reveal modes (instant or at the end) and a full history. |
| **Study assistant** | “Test me” mode with written answers, hints, explanations and keyword matching, plus multiple-choice practice with weak-topic tracking. No external AI is used. |
| **Battle mode** | Real-time multiplayer quiz rooms over SignalR, with live chat, invites and persistent rooms. |
| **Social** | Nicknames, avatars, friends, blocking and real-time notifications. |
| **Study groups** | Admin-managed groups. Members see the group's shared documents and can open them in the study assistant or start an exam. |
| **Document sharing** | Admins share documents with selected groups or with every registered user. Group access is evaluated dynamically. |
| **Admin panel** | Manage documents, groups and administrators. Every role change is audited and the last admin is protected. |
| **Auth** | ASP.NET Core Identity + JWT, Remember Me, password reset by email, and immediate token revocation on logout or role change. |
| **UI** | Responsive React interface with light and dark themes. |

## Tech stack

- **Backend:** ASP.NET Core 9 Minimal API, EF Core 9, SQL Server, ASP.NET Core Identity, JWT, SignalR
- **Parsing:** PdfPig (PDF) and a built-in DOCX (Office Open XML) reader, in a standalone library
- **Frontend:** React 19, TypeScript, Vite, Tailwind CSS v4, TanStack Query, React Router
- **Tests:** xUnit (parser and API logic)

## Project structure

```
src/
  ExamFlow.Api/        # Minimal API, feature folders (Auth, Documents, Exams, Study, Battles, Groups…)
  ExamFlow.Parsing/    # PDF/DOCX → questions (no EF, no HTTP)
tests/
  ExamFlow.Api.Tests/
  ExamFlow.Parsing.Tests/
client/                # React + Vite single-page app
```

## Getting started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- SQL Server (LocalDB, Express or Docker)
- EF Core CLI: `dotnet tool install --global dotnet-ef`

### 1. Configure secrets

```bash
cd src/ExamFlow.Api
dotnet user-secrets set "ConnectionStrings:Default" "Server=.;Database=ExamFlow;Trusted_Connection=True;TrustServerCertificate=True"
dotnet user-secrets set "Jwt:SigningKey" "<random string of at least 32 bytes>"
```

The API will not start without these settings.

### 2. Create the database

```bash
dotnet ef database update --project src/ExamFlow.Api
```

### 3. Run

```bash
dotnet run --project src/ExamFlow.Api     # API → http://localhost:5180
```

```bash
cd client
npm install
npm run dev                               # UI  → http://localhost:5190 (proxies /api)
```

## Development

```bash
dotnet build                              # 0 errors / 0 warnings expected
dotnet test                               # parser + API unit tests
cd client && npm run build && npx oxlint  # type-check, build, lint
```

In development, emails such as password-reset messages are written as `.eml` files to `src/ExamFlow.Api/App_Data/mail/`.

## Security notes

- Every user resource is filtered by the authenticated user. Another user's resource returns `404`.
- Correct answers are never sent to the client before the exam's reveal rules allow it.
- Roles are read from the database on every request, so a revoked admin loses access immediately.
- Secrets live only in user-secrets or environment variables and are never committed.
