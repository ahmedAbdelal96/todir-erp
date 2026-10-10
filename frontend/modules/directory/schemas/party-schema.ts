import { z } from "zod";

export const partyFormSchema = z.object({
  partyType: z.enum(["Person", "Organization"]),
  displayName: z.string().trim().min(2, "Name must be at least 2 characters").max(300),
  legalName: z.string().optional(), tradeName: z.string().optional(), nameAr: z.string().optional(), nameEn: z.string().optional(),
  firstName: z.string().optional(), middleName: z.string().optional(), lastName: z.string().optional(), countryCode: z.string().max(2).optional(), primaryLanguage: z.string().max(10).optional(),
  roles: z.array(z.enum(["Customer", "Supplier"])).min(1, "Select at least one role"), identifierType: z.string().optional(), identifierValue: z.string().optional(), identifierCountryCode: z.string().max(2).optional(),
  email: z.string().email("Invalid email address").optional().or(z.literal("")), phone: z.string().optional(), addressLine1: z.string().optional(), addressCity: z.string().optional(), addressCountryCode: z.string().max(2).optional()
});
export type PartyFormData = z.infer<typeof partyFormSchema>;
