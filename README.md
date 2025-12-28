# Kumari Cinemas Ticketing System 🎬

A premium, production-ready movie ticketing web application built with **ASP.NET Core** and **SQLite**.

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
  - **Database**: SQLite (Self-contained, Zero-config).
  - **Security**: CSRF Protection, SQL Injection Prevention, Secure Cookies.
  - **UI/UX**: Modern Dark Theme, Glassmorphism, Responsive Design.

---

## 🚀 Setup Instructions

### Prerequisites

1. **.NET 6.0 SDK** (or later).

### 1. Run the Application

The application automatically creates and seeds the local SQLite database on first run.

```bash
cd KumariCinemas.Web
dotnet run
```

Open your browser to: `http://localhost:5000` (or the URL shown in terminal).

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
  - `Data/`: Database Context and Initializer.
  - `KumariCinemas.db`: Local SQLite database file (created on runtime).

---

## 📝 Notes

- **Images**: Movie posters are loaded from external URLs (Unsplash). Internet connection required for images.
- **Seat Map**: Supports dynamic capacity up to 100 seats per hall (10x10 grid).
