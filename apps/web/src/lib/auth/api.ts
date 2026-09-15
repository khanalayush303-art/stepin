import { apiClient } from "./client";
import type { AuthUser, PasswordPolicy } from "./types";

const base = "/api/v1/auth";

export interface RegisterInput {
  email: string;
  password: string;
  confirmPassword: string;
  firstName: string;
  lastName: string;
  role: "Applicant" | "Recruiter";
}

export function register(input: RegisterInput) {
  return apiClient.post<{ message: string }>(`${base}/register`, input);
}

export function login(email: string, password: string, rememberMe = true) {
  return apiClient.post<AuthUser>(`${base}/login`, { email, password, rememberMe });
}

export function logout() {
  return apiClient.post<{ message: string }>(`${base}/logout`);
}

export function getCurrentUser() {
  return apiClient.get<AuthUser>(`${base}/me`);
}

export function getPasswordPolicy() {
  return apiClient.get<PasswordPolicy>(`${base}/password-policy`);
}

export function verifyEmail(email: string, token: string) {
  return apiClient.post<{ message: string }>(`${base}/verify-email`, { email, token });
}

export function resendVerification(email: string) {
  return apiClient.post<{ message: string }>(`${base}/resend-verification`, { email });
}

export function forgotPassword(email: string) {
  return apiClient.post<{ message: string }>(`${base}/forgot-password`, { email });
}

export function resetPassword(email: string, token: string, newPassword: string, confirmPassword: string) {
  return apiClient.post<{ message: string }>(`${base}/reset-password`, {
    email,
    token,
    newPassword,
    confirmPassword,
  });
}

export function completeAccountSetup(role: "Applicant" | "Recruiter") {
  return apiClient.post<{ redirectTo: string }>(`${base}/account-setup`, { role });
}

/** Top-level navigation, not a fetch — the OAuth challenge/redirect never goes through the JSON client. */
export function googleSignInHref(): string {
  return `${base}/external/google`;
}
