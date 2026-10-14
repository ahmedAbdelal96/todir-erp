import { z } from "zod";

export type PartyValidationKey =
  | "validation.displayNameMin"
  | "validation.displayNameMax"
  | "validation.emailInvalid"
  | "validation.personNamesForIndividuals";

export function createPartyFormSchema(translate: (key: PartyValidationKey) => string) {
  return z.object({
    partyType: z.enum(["Person", "Organization"]),
    displayName: z.string().trim().min(2, translate("validation.displayNameMin")).max(300, translate("validation.displayNameMax")),
    legalName: z.string().optional(),
    tradeName: z.string().optional(),
    nameAr: z.string().optional(),
    nameEn: z.string().optional(),
    firstName: z.string().optional(),
    middleName: z.string().optional(),
    lastName: z.string().optional(),
    countryCode: z.string().max(2).optional(),
    primaryLanguage: z.string().max(10).optional(),
    roles: z.array(z.enum(["Customer", "Supplier"])),
    identifierType: z.string().optional(),
    identifierValue: z.string().optional(),
    identifierCountryCode: z.string().max(2).optional(),
    email: z.string().email(translate("validation.emailInvalid")).optional().or(z.literal("")),
    phone: z.string().optional(),
    addressLine1: z.string().optional(),
    addressCity: z.string().optional(),
    addressCountryCode: z.string().max(2).optional()
  }).refine((data) => data.partyType !== "Organization" || (!data.firstName && !data.middleName && !data.lastName), {
    message: translate("validation.personNamesForIndividuals"),
    path: ["firstName"]
  });
}

export type PartyFormData = z.infer<ReturnType<typeof createPartyFormSchema>>;
