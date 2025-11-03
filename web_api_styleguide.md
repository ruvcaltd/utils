# .NET Core Web API Style & Best Practices Guide

---

## Section 1: Project & Architecture Structure

### 1. Solution Layout
- Use a **layered solution**:
  - `MyApp.Api` – Web API project (controllers, routing, request/response handling)
  - `MyApp.Core` – Core business logic (entities, interfaces, domain services)
  - `MyApp.Infrastructure` – EF Core DbContext, repositories, external services
  - `MyApp.Tests` – Unit and integration tests

### 2. Database Access
- Use **EF Core Database-First** approach.
- Keep scaffolded `DbContext` **as-is** with hardcoded connection string.
- Place **customizations in partial classes** in separate files/folders.
- Configure `DbContext` via **dependency injection** in `Program.cs` or `Startup.cs`.
- **Never modify auto-generated files** directly.

### 3. Business Logic
- All business rules must reside in a **dedicated Service Layer** (`Services` folder/project).
- Controllers must delegate operations to services.

### 4. Data Transfer
- **Never expose EF Core entities directly**.
- Map entities to **DTOs or ViewModels** for requests/responses.
- **Manual mapping** is preferred over AutoMapper.

---

## Section 2: Controllers & Endpoints

### 1. Base Controller
- Inherit all controllers from a **custom base controller** (`BaseApiController`) for:
  - Standardized response formatting
  - Shared logging
  - Common helper methods

### 2. Routing
- Use **attribute routing**:
  ```csharp
  [Route("api/[controller]")]
  [ApiController]
  public class UsersController : BaseApiController { ... }
  ```

### 3. Action Return Types
- Use **`ActionResult<T>`** for typed responses:
  ```csharp
  public ActionResult<UserDto> GetUser(int id)
  ```

### 4. Async Practices
- Async is optional; use for DB or external service calls as needed.

### 5. Controller Responsibilities
- Controllers handle **request validation, response formatting, HTTP concerns only**.
- **No business logic** in controllers.

---

## Section 3: Error Handling, Logging & Middleware

### 1. Exception Handling
- Handle exceptions **individually** in controllers or services.
- Map exceptions to appropriate HTTP responses.

### 2. Logging
- Use **`ILogger<T>`** for logging.
- Example:
  ```csharp
  _logger.LogInformation("Fetching user with ID {UserId}", userId);
  ```

### 3. Error Response Format
- Return errors in **consistent JSON**:
  ```json
  {
    "statusCode": 404,
    "message": "User not found",
    "details": "No user exists with ID 123"
  }
  ```

### 4. Middleware
- Register **exception handling middleware first**:
  ```csharp
  app.UseMiddleware<CustomExceptionMiddleware>();
  app.UseAuthentication();
  app.UseAuthorization();
  app.MapControllers();
  ```

---

## Section 4: Dependency Injection, Services & Repositories

### 1. Service Registration
- Register **all services and repositories individually**:
  ```csharp
  services.AddScoped<IUserService, UserService>();
  services.AddScoped<IUserRepository, UserRepository>();
  ```

### 2. Repository Pattern
- Implement **repository layer** to abstract DbContext.
- Services interact with repositories, not DbContext.

### 3. Service Lifetime
- Default: **Scoped**
- Singleton/Transient only if absolutely required.

### 4. Service Interfaces
- All services must have an **interface**.

---

## Section 5: Validation, DTOs & Mapping

### 1. Validation
- Use **FluentValidation** for request validation.
- Validators per DTO; no validation in controllers/services.

### 2. DTO Design
- DTOs are **read/write**.
- Never expose EF entities directly.

### 3. Mapping
- Centralize mapping in **service layer**.
- Prefer **manual mapping**.

### 4. Null Handling
- **Never return nulls** in DTOs.
- Use empty collections or default objects.

---

## Section 6: Security & Authentication/Authorization

### 1. Authentication
- Use **JWT Bearer Tokens**.

### 2. Authorization
- Use **role-based authorization**.
- Decorate controllers/actions with `[Authorize(Roles = "...")]`.

### 3. Secrets Management
- Use **environment variables**.
- Include **`set-env.ps1`** to set env variables consistently.

### 4. Password Handling
- Hash and salt passwords using **BCrypt**.
- Never store plain-text passwords.

---

## Section 7: Testing & CI/CD Standards

### 1. Unit Testing Framework
- Use **NUnit**.

### 2. Mocking
- Use **Moq**.

### 3. Integration Testing
- Use **in-memory DbContext**.
- Reset database between tests.

### 4. Code Coverage & Quality
- Focus on **critical paths and edge cases**.
- No strict coverage requirement.

---

## Section 8: API Documentation & Versioning

### 1. API Documentation
- Use **Swagger/OpenAPI** for all endpoints.
- Include **XML comments**.

### 2. API Versioning
- Use **URL-based versioning**:
  ```
  /api/v1/users
  ```

### 3. Endpoint Naming
- Follow **REST conventions** strictly:
  - `GET /users`
  - `POST /users`
  - `PUT /users/{id}`
  - `DELETE /users/{id}`

### 4. Deprecation
- Mark deprecated endpoints with `[Obsolete]`.
- Optionally return **warning headers**.

---

## Section 9: Logging, Telemetry & Monitoring

### 1. Logging Levels
- Flexible usage; no strict enforcement.

### 2. Telemetry
- Integrate **Application Insights**.

### 3. Structured Logging
- Plain text logging is sufficient.

### 4. Sensitive Data
- **Mask or omit sensitive data** in logs.

---

## Section 10: Caching, Performance & Pagination

### 1. Caching
- **No caching** by default.

### 2. Pagination
- Support **pagination for large datasets**.

### 3. Performance Guidelines
- Developers may optimize at discretion.

### 4. Async Queries
- DB queries should **support async**.

---

## Section 11: Version Control & Code Style

### 1. Branching Strategy
- Use **Git Flow**.

### 2. Commit Messages
- Use **descriptive messages**.

### 3. C# Code Style
- Enforce **`.editorconfig`** rules.

### 4. Naming Conventions
- Classes / Interfaces: PascalCase
- Methods: PascalCase
- DTOs: PascalCase
- Private Fields: `_camelCase`
- Constants: PascalCase

---

## Section 12: Miscellaneous Practices & Guidelines

### 1. Partial Classes
- Use **only as required**, mainly for EF customizations.

### 2. Folder / File Structure
- Organize **by layer**, not feature.

### 3. Comments / Documentation
- **XML docs required** for all public classes, methods, DTOs.

### 4. Third-Party Dependencies
- Minimize external dependencies; prefer **built-in .NET libraries**.