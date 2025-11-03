# React Application Style Guide

This document defines the standards and conventions for building React applications within our organization. It ensures consistency, maintainability, and scalability across all projects.

---

## 1. Project Structure

### **Feature-Based Folder Structure**
Organize the codebase by **features** rather than technical layers. Each feature should encapsulate its own components, hooks, context, and styles.

```
src/
  features/
    dashboard/
      components/
      hooks/
      context/
      DashboardPage.tsx
    users/
      components/
      hooks/
      context/
      UserList.tsx
  shared/
    components/
    hooks/
    utils/
  routes/
  App.tsx
  main.tsx
```

**Guidelines:**
- Each feature folder should contain everything related to that feature.
- Shared utilities, hooks, and UI components should live under `src/shared/`.
- Keep import paths consistent and use aliases (e.g. `@shared/*`).

---

## 2. Styling

### **Tailwind CSS**
Tailwind CSS is the preferred styling solution.

**Guidelines:**
- Use Tailwind utility classes directly in JSX.
- Avoid inline `style` attributes except for dynamic values.
- Use Tailwind config (`tailwind.config.js`) for theme customization.
- Reuse style patterns through **className helpers** or **Tailwind @apply** in minimal CSS files.

**Example:**
```tsx
export function Button({ label }: { label: string }) {
  return (
    <button className="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700">
      {label}
    </button>
  );
}
```

---

## 3. State Management

### **React Context API Only**
Use React’s built-in Context API for global or feature-level state management.

**Guidelines:**
- Keep contexts **feature-scoped** where possible.
- Create custom hooks for accessing context to enforce encapsulation.

**Example:**
```tsx
// src/features/auth/context/AuthContext.tsx
import { createContext, useContext, useState } from 'react';

interface AuthContextType {
  user: string | null;
  setUser: (user: string | null) => void;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<string | null>(null);
  return <AuthContext.Provider value={{ user, setUser }}>{children}</AuthContext.Provider>;
};

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used within an AuthProvider');
  return context;
}
```

---

## 4. Routing

### **React Router**
React Router is the standard for navigation.

**Guidelines:**
- Define routes in a centralized `src/routes/` directory.
- Use **lazy loading** for large feature modules.

**Example:**
```tsx
import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import { DashboardPage } from '@features/dashboard/DashboardPage';

const router = createBrowserRouter([
  { path: '/', element: <DashboardPage /> },
]);

export function App() {
  return <RouterProvider router={router} />;
}
```

---

## 5. Components

### **Functional Components Only (with Hooks)**
All components must be functional and use React hooks for lifecycle and state management.

**Guidelines:**
- Keep components **small and composable**.
- Use **TypeScript interfaces** or types for props.
- Prefer **explicit return types** for components.
- Use **React.memo** for expensive components.

**Example:**
```tsx
interface CardProps {
  title: string;
  children: React.ReactNode;
}

export const Card: React.FC<CardProps> = ({ title, children }) => (
  <div className="p-4 rounded-lg shadow bg-white">
    <h2 className="text-lg font-semibold mb-2">{title}</h2>
    {children}
  </div>
);
```

---

## 6. Linting & Formatting

### **ESLint + Prettier**
Linting and formatting must be enforced.

**Recommended config files:**

**`.eslintrc.js`**
```js
module.exports = {
  root: true,
  parser: '@typescript-eslint/parser',
  plugins: ['react', '@typescript-eslint', 'react-hooks'],
  extends: [
    'eslint:recommended',
    'plugin:react/recommended',
    'plugin:@typescript-eslint/recommended',
    'plugin:react-hooks/recommended',
    'prettier',
  ],
  settings: {
    react: { version: 'detect' },
  },
  rules: {
    'react/prop-types': 'off',
    '@typescript-eslint/no-unused-vars': ['warn'],
  },
};
```

**`.prettierrc`**
```json
{
  "semi": true,
  "singleQuote": true,
  "trailingComma": "all",
  "printWidth": 100
}
```

---

## 7. TypeScript

**Guidelines:**
- Always enable **strict mode** in `tsconfig.json`.
- Prefer **explicit types** for props, return values, and state.
- Avoid using `any` unless absolutely necessary.

---

## 8. Testing

**Guidelines:**
- Use **Jest** + **React Testing Library** for unit tests.
- Tests should reside next to components: `ComponentName.test.tsx`.

---

## 9. Performance & Best Practices

- Use `React.lazy` and `Suspense` for code splitting.
- Memoize expensive computations with `useMemo` and handlers with `useCallback`.
- Avoid unnecessary context re-renders by splitting providers when possible.

---

## 10. Exclusions

Code review and team workflow standards are intentionally excluded from this document.


## 11. Package Management

- Use **Yarn** for dependency management.
- Lock dependencies with `yarn.lock` — do not mix with `package-lock.json`.
- Always run `yarn install --frozen-lockfile` in CI environments.

## 12. API Clients

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