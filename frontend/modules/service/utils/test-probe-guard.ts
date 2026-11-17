export interface ProbeResponseLike {
  status: number;
  data?: { database?: string | undefined } | undefined;
  error?: string | undefined;
}

export interface VerifyTargetDatabaseOptions {
  probeResponse?: ProbeResponseLike | undefined;
  probeSecret?: string | undefined;
  expectedDatabase: string;
}

export function verifyTargetDatabase(options: VerifyTargetDatabaseOptions): {
  success: boolean;
  databaseName?: string | undefined;
  error?: string | undefined;
} {
  const { probeResponse, probeSecret, expectedDatabase } = options;

  if (!probeSecret || probeSecret.trim() === "") {
    return {
      success: false,
      error: "FAIL CLOSED: TODIR_TEST_PROBE_SECRET is required. No fallback credentials permitted."
    };
  }

  if (!probeResponse) {
    return {
      success: false,
      error: "FAIL CLOSED: Probe response was missing or network connection failed."
    };
  }

  if (probeResponse.status !== 200) {
    return {
      success: false,
      error: `FAIL CLOSED: Probe target returned HTTP ${probeResponse.status}. Refusing mutation tests.`
    };
  }

  const databaseName = probeResponse.data?.database?.trim().toLowerCase();
  if (!databaseName) {
    return {
      success: false,
      error: "FAIL CLOSED: Target database probe returned empty or invalid database name."
    };
  }

  if (databaseName === "todir") {
    return {
      success: false,
      error: 'FAIL CLOSED: Target API is connected to populated development database ("todir"). Mutation tests strictly prohibited.'
    };
  }

  if (databaseName.includes("prod") || databaseName === "todir_production") {
    return {
      success: false,
      error: `FAIL CLOSED: Target API is connected to production database ("${databaseName}"). Mutation tests strictly prohibited.`
    };
  }

  const expectedLower = expectedDatabase.trim().toLowerCase();
  if (databaseName !== expectedLower) {
    return {
      success: false,
      error: `FAIL CLOSED: Target database "${databaseName}" does not match explicitly authorized test database "${expectedLower}".`
    };
  }

  return { success: true, databaseName };
}
