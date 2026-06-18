-- Эталонная структура доменных таблиц (соответствует моделям EF).
-- При подходе Database First: создайте эти таблицы вручную, затем выполните:
-- Scaffold-DbContext "Server=..." Microsoft.EntityFrameworkCore.SqlServer -OutputDir Models/Scaffold -Force
-- Таблицы ASP.NET Identity создаются миграциями отдельно.

CREATE TABLE Departments (
    Id INT IDENTITY PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL
);

CREATE TABLE Employees (
    Id INT IDENTITY PRIMARY KEY,
    FullName NVARCHAR(200) NOT NULL,
    DepartmentId INT NOT NULL REFERENCES Departments(Id),
    Position NVARCHAR(100) NOT NULL DEFAULT N'',
    WorkScheduleType INT NOT NULL DEFAULT 0,
    HourlyRate DECIMAL(18,2) NOT NULL DEFAULT 0
);

CREATE TABLE WorkDays (
    Id INT IDENTITY PRIMARY KEY,
    EmployeeId INT NOT NULL REFERENCES Employees(Id) ON DELETE CASCADE,
    [Date] DATE NOT NULL,
    StartTime DATETIME2 NULL,
    EndTime DATETIME2 NULL,
    LunchBreakMinutes INT NOT NULL DEFAULT 0,
    IsAbsent BIT NOT NULL DEFAULT 0,
    AbsenceReason NVARCHAR(200) NULL,
    CONSTRAINT UQ_WorkDays_Emp_Date UNIQUE (EmployeeId, [Date])
);

CREATE TABLE Overtime (
    Id INT IDENTITY PRIMARY KEY,
    WorkDayId INT NOT NULL UNIQUE REFERENCES WorkDays(Id) ON DELETE CASCADE,
    OvertimeHours DECIMAL(18,2) NOT NULL DEFAULT 0,
    NightHours DECIMAL(18,2) NOT NULL DEFAULT 0
);

CREATE TABLE WorkScheduleExceptions (
    Id INT IDENTITY PRIMARY KEY,
    ExceptionDate DATE NOT NULL UNIQUE,
    IsHoliday BIT NOT NULL DEFAULT 0,
    Description NVARCHAR(200) NULL
);
