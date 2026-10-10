export type CapabilityState =
  | "Available"
  | "Unavailable"
  | "ReadOnly"
  | "NotActivated"
  | "TemporarilyUnavailable";

export interface CapabilityDetail {
  readonly state: CapabilityState;
  readonly reason?: string | undefined;
}

export class CapabilitySet {
  private readonly capabilities: ReadonlyMap<string, CapabilityDetail>;

  constructor(capabilities: Record<string, CapabilityState | CapabilityDetail> = {}) {
    const map = new Map<string, CapabilityDetail>();
    for (const [key, value] of Object.entries(capabilities)) {
      if (typeof value === "string") {
        map.set(key, { state: value });
      } else {
        map.set(key, value);
      }
    }
    this.capabilities = map;
  }

  isAvailable(capabilityKey: string): boolean {
    return this.capabilities.get(capabilityKey)?.state === "Available";
  }

  isReadOnly(capabilityKey: string): boolean {
    return this.capabilities.get(capabilityKey)?.state === "ReadOnly";
  }

  isActivated(capabilityKey: string): boolean {
    const state = this.capabilities.get(capabilityKey)?.state;
    return state === "Available" || state === "ReadOnly";
  }

  getState(capabilityKey: string): CapabilityState {
    return this.capabilities.get(capabilityKey)?.state ?? "Unavailable";
  }

  getReason(capabilityKey: string): string | undefined {
    return this.capabilities.get(capabilityKey)?.reason;
  }

  toMap(): Record<string, CapabilityDetail> {
    const obj: Record<string, CapabilityDetail> = {};
    for (const [key, val] of this.capabilities.entries()) {
      obj[key] = val;
    }
    return obj;
  }
}
