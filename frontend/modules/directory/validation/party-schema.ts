import { z } from "zod";

export const partyFormSchema = z.object({
  partyType: z.enum(["Person", "Organization"]),
  displayName: z.string().trim().min(2, "الاسم المعروض يجب ألا يقل عن حرفين").max(300, "الاسم لا يتجاوز 300 حرف"),
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
  email: z.string().email("صيغة البريد الإلكتروني غير صحيحة").optional().or(z.literal("")),
  phone: z.string().optional(),
  addressLine1: z.string().optional(),
  addressCity: z.string().optional(),
  addressCountryCode: z.string().max(2).optional()
}).refine((data) => {
  if (data.partyType === "Organization") {
    // If organization, first/middle/last name should not be provided
    return !data.firstName && !data.middleName && !data.lastName;
  }
  return true;
}, {
  message: "حقول الاسم الشخصي مخصصة للأفراد فقط وليس للمنشآت",
  path: ["firstName"]
});

export type PartyFormData = z.infer<typeof partyFormSchema>;
