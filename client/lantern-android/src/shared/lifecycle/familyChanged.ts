const listeners = new Set<() => void>();

export function onFamilyChanged(listener: () => void) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function notifyFamilyChanged() {
  listeners.forEach((listener) => listener());
}
