# Kumari Cinemas Ticketing System - Final Project Report

## 1. Introduction

This project implements a web-based database application for Kumari Cinemas, a cinema chain in Nepal. The goal is to provide a centralized ticket management system that addresses existing discrepancies in location, hall management, and operational costs.

## 2. Database Design & Analysis

### 2.1 Normalization

The database was designed following **Third Normal Form (3NF)** principles to ensure data integrity.

- **1NF**: Atomic values in all columns (e.g., separating user names and addresses).
- **2NF**: No partial dependencies on the primary key (e.g., movie details are in a separate `M_Movies` table).
- **3NF**: No transitive dependencies (e.g., Hall details depend on the HallId, not the ShowId).

### 2.2 Entity Relationship Diagram (ERD)

The system comprises six main entities: Users, Branches, Halls, Movies, Shows, and Bookings. The conceptual model was designed using **Oracle SQL Developer Data Modeler** to ensure 3NF compliance. Relationships were established to allow a one-to-many flow from physical infrastructure (Branches -> Halls) and content (Movies -> Shows) into the transaction layer (Shows -> Bookings).

## 3. System Implementation

### 3.1 Dynamic Pricing

The system implements a custom business logic for dynamic pricing:

- **New Release Logic**: Movies released within the last 7 days incur a 20% surcharge.
- **Holiday Logic**: Bookings on public holidays incur a 15% surcharge.

### 3.2 Automated Cancellation

Per the requirement, a background service (`BookingCleanupService`) monitors the database every 15 minutes. It automatically cancels any booking with a 'BOOKED' status that is within 1 hour of the showtime and has not been finalized (PAID).

### 3.3 Data Integrity & Validation

To prevent overbooking, the system implements a real-time capacity check. Before any booking is confirmed, the system queries the **M_Halls** capacity and compares it against the existing **TotalTickets** for that specific show. If the hall is full, the transaction is rejected, ensuring physical constraints are respected.

### 3.3 UI/UX Design

The web frontend was built using ASP.NET Core MVC with a focus on modern design principles:

- **Dark Mode**: Reduces eye strain and offers a cinematic feel.
- **Glassmorphism**: Provides a premium, sophisticated look.
- **Responsive Layout**: Ensures the system is usable on both desktop and mobile devices.

## 4. Conclusion

The prototype successfully meets all core requirements of the KumariCinemas case study, providing a robust, automated, and user-friendly solution for modern cinema management.
