# Kumari Cinemas Ticketing System 🎬

A premium, production-ready movie ticketing web application built with **ASP.NET Core** and **Oracle Database**.

## ✨ Features

- **User & Authentication**
  - Secure Registration & Login with Password Hashing.
  - Role-based Access Control (Admin vs User).
  - "My Bookings" Dashboard.

- **Booking Flow**
  - 🎥 **Browse Movies**: Search by title, filter by genre or date.
  - 💺 **Interactive Seat Selection**: Visual seat map with real-time availability.
  - 💳 **Payment Integration**: Secure mock payment flow with validation.
  - 🎫 **Instant Booking**: Real-time capacity checks and concurrency handling.

- **Admin Dashboard**
  - Manage Movies (Add/Edit/Delete).
  - Schedule Shows (Date, Time, Price).
  - View Booking Statistics.

- **Technical Highlights**
  - **Architecture**: MVC Pattern with Service Layer.
  - **Database**: 3NF Normalized Oracle Schema.
  - **Security**: CSRF Protection, SQL Injection Prevention, Secure Cookies.
  - **UI/UX**: Modern Dark Theme, Glassmorphism, Responsive Design.

---

## 🚀 Setup Instructions

### Prerequisites

1. **Oracle Database XE 21c** (or compatible) running locally.
2. **.NET 6.0 SDK** (or later).

### 1. Database Setup

A single script is provided to set up the entire database (Schema + Test Data).

#### Option A: Using PowerShell (Recommended)

This script will prompt you for your `SYSTEM` password securely.

```powershell
cd Database
.\SetupDatabase.ps1
```

#### Option B: Manual Setup (SQL Developer)

1. Open **Oracle SQL Developer**.
2. Connect to your database (User: `SYSTEM`, using your configured password).
3. Open `Database/setup_database.sql`.
4. Run the script (F5).

### 2. Configure Application Credentials

> [!IMPORTANT]
> You must configure your database password before running the app.

1. Open `KumariCinemas.Web/appsettings.json`.
2. Replace `[YOUR_PASSWORD]` with your actual Oracle `SYSTEM` password.

### 3. Run the Application

```bash
cd KumariCinemas.Web
dotnet run
```

Open your browser to: `https://localhost:7198` (or the URL shown in terminal).

---

## 🔑 Default Credentials

### Admin Account

- **Username**: `admin`
- **Password**: `password`
- **Role**: Full access to dashboard and management features.

### User Account (Test)

You can register a new account, or use:

- (No default user created strictly, feel free to Register!)

---

## 🛠 Project Structure

- `KumariCinemas.Web`: Main ASP.NET Core MVC Application.
  - `Controllers/`: Standard MVC Controllers.
  - `Views/`: Razor Views with CSS/JS.
  - `wwwroot/`: Static assets (CSS, JS, Images).
- `Database`: DB Scripts.
  - `setup_database.sql`: The master script.

---

## 📝 Notes

- **Images**: Movie posters are loaded from external URLs (Unsplash). Internet connection required for images.
- **Seat Map**: Supports dynamic capacity up to 100 seats per hall (10x10 grid).
