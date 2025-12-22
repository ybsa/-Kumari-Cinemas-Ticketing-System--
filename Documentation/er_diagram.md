# Database ER Diagram (Conceptual)

This diagram represents the normalized 3NF schema I implemented. You can use this as a reference when building your model in **Oracle SQL Developer Data Modeler**.

```mermaid
erDiagram
    M_Users ||--o{ T_Bookings : "makes"
    M_Branches ||--|{ M_Halls : "contains"
    M_Movies ||--|{ M_Shows : "has"
    M_Halls ||--|{ M_Shows : "hosts"
    M_Shows ||--o{ T_Bookings : "has"
    T_Bookings ||--|{ T_Tickets : "consists of"

    M_Users {
        number UserId PK
        string Username
        string Password
        string Address
        string Role
    }

    M_Branches {
        number BranchId PK
        string BranchName
        string Location
    }

    M_Halls {
        number HallId PK
        number BranchId FK
        string HallName
        number Capacity
    }

    M_Movies {
        number MovieId PK
        string Title
        string Duration
        string Language
        string Genre
        date ReleaseDate
    }

    M_Shows {
        number ShowId PK
        number MovieId FK
        number HallId FK
        date ShowDate
        string StartTime
        number BasePrice
    }

    T_Bookings {
        number BookingId PK
        number UserId FK
        number ShowId FK
        timestamp BookingTime
        string Status
        number FinalPrice
    }

    T_Tickets {
        number TicketId PK
        number BookingId FK
        string SeatNumber
    }
```

## Why this design?

- **Normalization (3NF)**: By separating Branches, Halls, and Shows, we avoid data redundancy. For example, the Hall capacity is stored once, even if multiple different movies show in that hall.
- **Relational Integrity**: Foreign keys ensure that bookings cannot exist for non-existent users or shows.
- **Scalability**: This design allows KumariCinemas to easily add more branches or halls without changing the application logic.
