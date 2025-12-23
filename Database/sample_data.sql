-- Sample Data for KumariCinemas (Based on Assignment Figure 1)

-- Insert Companies/Branches
INSERT INTO M_Branches (BranchName, Location) VALUES ('Pokhara Cineplex', 'Pokhara');
INSERT INTO M_Branches (BranchName, Location) VALUES ('Labim Mall', 'Kathmandu');
INSERT INTO M_Branches (BranchName, Location) VALUES ('Biratnagar Cineplex', 'Biratnagar');

-- Insert Movie
INSERT INTO M_Movies (Title, Duration, Language, Genre, ReleaseDate) 
VALUES ('Avatar: Fire and Ash', 'n/a', 'English', 'Fiction', TO_DATE('2025-12-19', 'YYYY-MM-DD'));

-- Insert Halls
INSERT INTO M_Halls (BranchId, HallName, Capacity) VALUES (1, 'Hall 1', 326);
INSERT INTO M_Halls (BranchId, HallName, Capacity) VALUES (2, 'Hall 1', 182);
INSERT INTO M_Halls (BranchId, HallName, Capacity) VALUES (3, 'Hall 1', 200);

-- Insert Shows (Sample Morning Shows)
INSERT INTO M_Shows (MovieId, HallId, ShowDateTime, BasePrice) 
VALUES (1, 1, TO_TIMESTAMP('2025-12-21 10:00:00', 'YYYY-MM-DD HH24:MI:SS'), 390);

INSERT INTO M_Shows (MovieId, HallId, ShowDateTime, BasePrice) 
VALUES (1, 2, TO_TIMESTAMP('2025-12-21 13:00:00', 'YYYY-MM-DD HH24:MI:SS'), 420);

INSERT INTO M_Shows (MovieId, HallId, ShowDateTime, BasePrice) 
VALUES (1, 3, TO_TIMESTAMP('2025-12-21 18:00:00', 'YYYY-MM-DD HH24:MI:SS'), 200);

-- Insert Sample User
-- NOTE: Password is stored as PLAINTEXT here for demonstration.
-- In production, passwords are hashed by the application.
-- To test: Register a new user via the website, or use this user
-- after modifying UserController to accept plaintext (for demo only).
INSERT INTO M_Users (Username, Password, Address) 
VALUES ('Pratibha Gurung', 'password123', 'Pokhara');

COMMIT;
