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
INSERT INTO M_Shows (MovieId, HallId, ShowDate, StartTime, BasePrice) 
VALUES (1, 1, TO_DATE('2025-12-21', 'YYYY-MM-DD'), 'Morning', 390);

INSERT INTO M_Shows (MovieId, HallId, ShowDate, StartTime, BasePrice) 
VALUES (1, 2, TO_DATE('2025-12-21', 'YYYY-MM-DD'), 'Morning', 420);

INSERT INTO M_Shows (MovieId, HallId, ShowDate, StartTime, BasePrice) 
VALUES (1, 3, TO_DATE('2025-12-21', 'YYYY-MM-DD'), 'Morning', 200);

-- Insert Sample User
INSERT INTO M_Users (Username, Password, Address) 
VALUES ('Pratibha Gurung', 'password123', 'Pokhara');

COMMIT;
