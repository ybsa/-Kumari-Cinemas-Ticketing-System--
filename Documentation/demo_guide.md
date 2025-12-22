# Kumari Cinemas - Project Demo Guide

**Step-by-Step Implementation & Presentation**

This document provides the exact steps to set up, run, and present your movie ticketing system.

---

## Part 1: Database Setup (Oracle SQL)

*The database is the backbone of your system. You must load this first.*

1. **Open Oracle SQL Developer**: Launch the application on your computer.
2. **Connect**: Log in to your Oracle database instance.
3. **Run Schema Script**:
    - Go to `File` > `Open`.
    - Select: `Database/database_schema.sql`.
    - Press **F5** to run the script. This creates your tables (Users, Movies, Halls, Shows, Bookings).
4. **Run Sample Data**:
    - Select: `Database/sample_data.sql`.
    - Press **F5**. This adds 'Avatar', 'Pokhara Cineplex', and more to your system.
5. **Commit**: Ensure you see "Commit complete" in the log.

---

## Part 2: Application Setup (Visual Studio)

*This launches the premium web interface.*

1. **Open Visual Studio 2022**: Open the program.
2. **Open Project**: Select "Open a project or solution" and navigate to the `KumariCinemas.Web` folder.
3. **Configure Connection**:
    - Open `appsettings.json`.
    - Update the `OracleDb` connection string with your specific Host, Port, Service Name, User ID, and Password.
4. **Build and Run**:
    - Press **F5** (the green play button).
    - Your default browser will open to the home page of Kumari Cinemas.

---

## Part 3: The Demo Script (How to Present)

*Use these talking points to impress your professors.*

### 1. The Design (Visual Excellence)
>
> "As you can see, the UI uses a premium **Dark Mode** with **Glassmorphism** effects. This is designed to give the user a high-end cinematic experience from the moment they land on the page."

### 2. Dynamic Pricing (The 'Smart' Feature)
>
> "Our system features an automated pricing engine. For example, 'Avatar' is a **New Release** (released within the last 7 days), so the system automatically adds a **20% premium** to the base price of Rs. 390, making it Rs. 468 without any manual input."

### 3. Automated Cancellation (The Logic)
>
> "To solve the problem of unpaid reservations, we have a **Background Service** running. If a user books a ticket but doesn't pay, the system will automatically find that record and cancel it exactly **1 hour before showtime**, freeing up the seat for other paying customers."

### 4. Database Integrity (3NF)
>
> "Technically, the backend is fully normalized to **3rd Normal Form (3NF)**. This means branches, halls, and showtimes are stored efficiently to prevent data errors and allow for easy scaling."

---

## Part 4: Troubleshooting

- **Database Connection Error**: Verify your `appsettings.json` credentials. Ensure the Oracle listener is running.
- **Port Conflicts**: If the app won't start, ensure no other web service is using the same port.
