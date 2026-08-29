import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import "./index.css";
import App from "./App.tsx";
import { SessionProvider } from "./context/SessionContext";
import { AdminSessionProvider } from "./context/AdminSessionContext";
import { ThemeProvider } from "./context/ThemeContext";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <ThemeProvider>
      <BrowserRouter>
        <SessionProvider>
          <AdminSessionProvider>
            <App />
          </AdminSessionProvider>
        </SessionProvider>
      </BrowserRouter>
    </ThemeProvider>
  </StrictMode>,
);
