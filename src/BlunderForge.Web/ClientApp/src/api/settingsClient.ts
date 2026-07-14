export interface AiProviderSettingsDto {
  provider: string;
  baseUrl: string | null;
  interactiveModel: string;
  reviewModel: string;
  timeoutSeconds: number;
  maxRetryCount: number;
  configured: boolean;
  secretAvailable: boolean;
  secretSource: string;
}

export interface UpdateAiProviderSettingsRequestDto {
  provider?: string;
  baseUrl?: string;
  interactiveModel?: string;
  reviewModel?: string;
  timeoutSeconds?: number;
  maxRetryCount?: number;
}

async function apiFetch<T>(url: string, options?: RequestInit): Promise<T> {
  const response = await fetch(url, options);

  if (!response.ok) {
    let detail = response.statusText;
    try {
      const errorBody = (await response.json()) as { detail: string };
      detail = errorBody.detail;
    } catch {
      // response body was not valid JSON; use defaults
    }
    throw new Error(detail);
  }

  return response.json() as Promise<T>;
}

export async function getAiProviderSettings(signal?: AbortSignal): Promise<AiProviderSettingsDto> {
  return apiFetch<AiProviderSettingsDto>("/api/settings/ai-provider", { signal });
}

export async function updateAiProviderSettings(request: UpdateAiProviderSettingsRequestDto): Promise<AiProviderSettingsDto> {
  return apiFetch<AiProviderSettingsDto>("/api/settings/ai-provider", {
    method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(request)
  });
}

export async function testAiProvider(): Promise<{ available: boolean; detail: string }> {
  return apiFetch<{ available: boolean; detail: string }>("/api/settings/ai-provider/test", { method: "POST" });
}
