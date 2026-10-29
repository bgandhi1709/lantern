export const performance = { dataCollectionEnabled: false };

export const getPerformance = () => performance;

export function resetFakePerf() {
  performance.dataCollectionEnabled = false;
}
