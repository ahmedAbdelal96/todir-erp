/**
 * Level 2: Semantic Tokens
 * Expresses product meaning and intent rather than raw physical palette values.
 */

export interface SemanticSurfaceTokens {
  readonly page: string;
  readonly card: string;
  readonly elevated: string;
  readonly subtle: string;
  readonly inset: string;
  readonly overlay: string;
}

export interface SemanticContentTokens {
  readonly primary: string;
  readonly secondary: string;
  readonly muted: string;
  readonly inverse: string;
  readonly disabled: string;
}

export interface SemanticBorderTokens {
  readonly default: string;
  readonly subtle: string;
  readonly strong: string;
  readonly focus: string;
  readonly interactive: string;
}

export interface SemanticActionTokens {
  readonly primary: string;
  readonly primaryHover: string;
  readonly primaryActive: string;
  readonly primaryContent: string;
  readonly secondary: string;
  readonly secondaryHover: string;
  readonly secondaryActive: string;
  readonly secondaryContent: string;
  readonly ghost: string;
  readonly ghostHover: string;
  readonly ghostActive: string;
  readonly ghostContent: string;
}

export interface SemanticStatusTone {
  readonly base: string;
  readonly subtle: string;
  readonly content: string;
  readonly border: string;
}

export interface SemanticStatusTokens {
  readonly success: SemanticStatusTone;
  readonly warning: SemanticStatusTone;
  readonly danger: SemanticStatusTone;
  readonly info: SemanticStatusTone;
}

export interface SemanticTokens {
  readonly surface: SemanticSurfaceTokens;
  readonly content: SemanticContentTokens;
  readonly border: SemanticBorderTokens;
  readonly action: SemanticActionTokens;
  readonly status: SemanticStatusTokens;
}
