# StudentPlannerApp

StudentPlannerApp is a simple ASP.NET Core MVC web application made as a student project.
Its purpose is to help a student organize study tasks by subject and keep short notes.

## Main Features

- Home page with basic statistics
- CRUD operations for study tasks
- Subject list page
- Server-side and client-side validation
- Entity Framework Core with SQL Server
- Bootstrap layout with navigation and reusable partial views

## Technologies Used

- ASP.NET Core MVC (.NET 8)
- Entity Framework Core
- SQL Server / LocalDB
- Razor Views
- Bootstrap 5

## Entities

1. Subject
2. StudyTask
3. StudyNote

## How to Run

1. Open the project folder in Visual Studio or Rider.
2. Restore NuGet packages.
3. Make sure SQL Server LocalDB is available.
4. Run the project.

The app is configured to use this default connection string in `appsettings.json`:

`Server=(localdb)\MSSQLLocalDB;Database=StudentPlannerAppDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True`

If needed, you can change the connection string to a normal SQL Server instance.

## Database

A first migration is already included in the project.
On startup the application calls `Database.Migrate()` and also seeds a few sample records.

## Suggested Git Commits

To make the repository history look realistic, split the work into several smaller commits, for example:

1. Create initial MVC project structure
2. Add entity models and DbContext
3. Add initial migration
4. Implement task service and subject service
5. Add StudyTasks controller CRUD actions
6. Create task views
7. Add home page and subject page
8. Add validation and polish forms
9. Add seed data and styling
10. Write README and final cleanup

## Notes

- The project is intentionally kept simple and readable.
- It avoids unnecessary complexity so it looks like normal student work.
