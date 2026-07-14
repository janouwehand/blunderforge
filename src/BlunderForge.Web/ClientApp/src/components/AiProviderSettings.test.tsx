import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { vi } from "vitest";
import { AiProviderSettings } from "./AiProviderSettings";

const settings = { provider: "DeepSeek", baseUrl: "https://stub.test", interactiveModel: "fast", reviewModel: "strong", timeoutSeconds: 30, maxRetryCount: 1, secretAvailable: true, secretSource: "BLUNDERFORGE_DEEPSEEK_API_KEY_FILE" };

test("shows and saves only non-secret provider settings", async () => {
  const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValue({ ok: true, json: async () => settings } as Response);
  render(<AiProviderSettings />);
  expect(await screen.findByDisplayValue("https://stub.test")).toBeInTheDocument();
  expect(screen.queryByLabelText(/api.?key/i)).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole("button", { name: "Save" }));
  await waitFor(() => expect(fetchMock).toHaveBeenCalledWith("/api/settings/ai-provider", expect.objectContaining({ method: "PUT" })));
  expect(JSON.stringify(fetchMock.mock.calls)).not.toContain("fake-secret");
  fetchMock.mockRestore();
});
