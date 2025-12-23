# Kumari Cinemas Ticketing System

A web-based movie ticketing system for Kumari Cinemas, built with **ASP.NET Core MVC** and **Oracle SQL**.

## Project Structure

```
├── Database/               # Oracle SQL scripts
│   ├── database_schema.sql # Table definitions (8 tables)
│   └── sample_data.sql     # Sample movies, halls, shows
│
├── KumariCinemas.Web/      # ASP.NET Core MVC Application
│   ├── Controllers/        # HomeController, UserController, TicketController, ReviewController
│   ├── Models/             # Movie, Show, Booking, Review
│   ├── Services/           # PricingHelper, PasswordHelper, BookingCleanupService
│   └── Views/              # Razor views for UI
│
└── README.md               # This file
```

## Key Features

- **User Registration & Login** (Secure password hashing)
- **Browse Movies** with dynamic pricing
- **Book Tickets** with seat availability check
- **My Bookings** page to view booking history
- **Submit Reviews** for movies
- **Background Service** to auto-cancel unpaid bookings

## Database Setup

1. Install Oracle Database XE
2. Open SQL Developer
3. Run `Database/database_schema.sql`
4. Run `Database/sample_data.sql`

## Run Application

1. Open in Visual Studio
2. Press F5 to run
3. Click "Register" to create a new account
4. Login with your new account
