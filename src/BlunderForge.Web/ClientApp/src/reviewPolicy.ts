export function shouldRequestAutomaticReview(status: string, aiAvailable: boolean, useAi: boolean) {
  return status !== "Active" && aiAvailable && useAi;
}
