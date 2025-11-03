# 🧭 Angular Style & Best Practices Guide

This guide defines the conventions, patterns, and standards that all coding agents and contributors should follow when developing Angular applications.  
It emphasizes **clarity, consistency, maintainability**, and **modern Angular practices** (v20+ with zoneless change detection and signals).

---

## 📦 Package Management

- Use **Yarn** for dependency management.
- Lock dependencies with `yarn.lock` — do not mix with `package-lock.json`.
- Always run `yarn install --frozen-lockfile` in CI environments.

---

## 🧱 Project Structure

### Folder Layout

- Use a **flat feature folder layout** for clarity and scalability:
  ```
  src/
    api/
    app/
      users/
        user-list/
        user-detail/
      orders/
        order-list/
      shared/
        ui/
      core/
  ```

- Group files by **feature**, not by type.
- Keep related component files (`.ts`, `.html`, `.scss`) together.
- Place all UI-related code in `src/` and configuration/scripts outside it.

### Modules

- Use **standalone components** across the app (modern Angular approach).
- Keep two modules only:
  - `CoreModule` — singleton services, guards, interceptors.
  - `SharedModule` — reusable UI components, directives, and pipes.

---

## 🧩 Typing & Language Rules

- Use **strict TypeScript mode** (`strict: true`, `strictTemplates: true`).
- Avoid `any` unless absolutely unavoidable.
- Prefer `readonly` for immutable properties.
- Use **explicit access modifiers** (`public`, `protected`, `private`) for all class members.
- Protect template-only properties with `protected`.

---

## 🧠 State Management

- Use **NgRx with Signals** for reactive, predictable state handling.
- Organize store slices by feature area.
- Keep selectors, reducers, and actions colocated within their feature directories.

---

## 🌐 API Clients

- API clients are **auto-generated** using **NSwag**.
- To regenrate the client after the API has been changed run the following in the root folder of the app:
  nswag run nswag.json
- If nswag tool is not available, install it using yarm
- Always placed under:  
  ```
  src/api/
  ```
- Do **not modify generated code directly**.  

---

## 🎨 Styling

- Use **SCSS** for maintainable, structured styles.
- Apply **TailwindCSS** for utilities, spacing, responsiveness, and layout.
- Prefer component-scoped styles.
- Follow a **BEM-like** convention for custom class names.

---

## 🧱 Components & Directives

- Components should focus on **presentation logic**; delegate business logic to services.
- Prefer the **`inject()`** function for dependency injection instead of constructor parameters.
- Group Angular-specific properties (inputs, outputs, queries) **before methods** in classes.
- Keep methods small and cohesive — one clear responsibility each.
- Use **Zoneless Angular** performance model (Angular v20+).
- Use `protected` for members accessed by templates, `private` otherwise.

---

## ⚡ Performance & Reactivity

- Use **Zoneless Angular (v20+)** — remove `zone.js` dependency entirely.
- Use `async` pipe in templates for all observable bindings.
- Avoid manual `.subscribe()` calls unless absolutely necessary.
- Favor `signals` and computed properties over subjects where appropriate.
- Refactor heavy template logic into TypeScript code.

---

## 🧩 Dependency Injection

- Prefer **tree-shakable** services with `providedIn: 'root'`.
- Use `CoreModule` only for true singletons that can’t be tree-shaken.
- Avoid unnecessary providers in components or feature folders unless isolation is intentional.

---

## 🧪 Testing

- Use the **default Angular testing framework (Karma + Jasmine)**.
- End test files with `.spec.ts`.
- Write **unit tests** for all components, services, and utilities.
- Keep tests colocated with implementation files.

---

## 🧰 Linting & Formatting

- Enforce **ESLint** for code quality and consistency.
- Enforce **Prettier** for formatting.
- Include lint checks and formatting in CI/CD pipelines.
- Never disable ESLint rules without justification in code reviews.

---

## 🧱 Templates & Event Handling

- Use `[class]` and `[style]` bindings instead of `NgClass` / `NgStyle` for simple cases.
- Name event handlers **by intent**, not by DOM event name (e.g., `saveUser()` instead of `onClick()`).
- Avoid complex template logic — move it into component code.

---

## ⚙️ Lifecycle & Structure

- Implement lifecycle hook interfaces (e.g., `OnInit`, `OnDestroy`) for type safety.
- Keep lifecycle hooks clean; delegate complex logic to helper functions.
- Order class members:
  1. Inputs, Outputs, Queries  
  2. Injected Dependencies  
  3. Public Properties  
  4. Protected/Private Properties  
  5. Lifecycle Hooks  
  6. Methods  

---

## 🧭 General Principles

- **Prioritize consistency** within each file and feature.
- Keep files **small and focused** — one concept per file.
- Follow a **feature-first** mindset — group by business domain.
- Use descriptive, hyphen-separated filenames (e.g., `user-profile.component.ts`).
- Prefer **clarity over cleverness** — optimize for future maintainers.

---

## 🧱 Example Feature Folder Structure

A practical example for a `users` feature:

```
src/app/users/
  user-list/
    user-list.component.ts
    user-list.component.html
    user-list.component.scss
    user-list.component.spec.ts
    user-list.store.ts           # NgRx Signal store for list state
  user-detail/
    user-detail.component.ts
    user-detail.component.html
    user-detail.component.scss
  services/
    user-data.service.ts         # Wraps NSwag API client
  models/
    user.model.ts
  index.ts                       # Barrel file for exports
```

- Components are standalone, feature-scoped.
- Services and models live inside their feature folders.
- Shared reusable UI goes in `src/app/shared/ui/`.

---

## 🚫 Exclusions

This guide intentionally **excludes**:
- Commit message conventions (e.g., Conventional Commits)
- Pull Request review processes
- Branching and release strategies

These should be defined separately in a **Development Process Guide**.

---

## ✅ Summary of Key Standards

| Area | Standard |
|------|-----------|
| **Angular Version** | ≥ 20 (Zoneless) |
| **Package Manager** | Yarn |
| **Component Type** | Standalone |
| **State Management** | NgRx + Signals |
| **Styling** | SCSS + Tailwind |
| **Linting** | ESLint + Prettier |
| **Testing** | Karma + Jasmine |
| **API Layer** | NSwag auto-generated (src/api) |
| **Structure** | Flat feature-based folders |
| **DI Strategy** | providedIn: 'root' |
| **Strict Mode** | Enabled |

---

> ✨ **Guiding Principle:**  
> “Consistency is more important than perfection.  
> If a choice improves clarity and maintainability, prefer it — even if it deviates from the default.”
> Build often and ensure there are no compilation errors

---

_Last updated: 2025-10-31_
