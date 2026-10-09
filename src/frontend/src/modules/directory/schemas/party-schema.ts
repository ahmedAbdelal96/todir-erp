import { z } from "zod";

export const partyFormSchema = z.object({
  partyType: z.enum(["Person", "Organization"]),
  displayName: z.string().min(2, "Name must be at least 2 characters"),
  legalName: z.string().optional(),
  tradeName: z.string().optional(),
  taxId: z.string().optional(),
  nationalId: z.string().optional(),
  primaryEmail: z.string().email("Invalid email address").optional().or(z.literal("")),
  primaryPhone: z.string().optional(),
  roles: z.array(z.enum(["Customer", "Vendor", "Employee", "Partner"])).min(1, "Select at least one role")
});

export type PartyFormData = z.infer<typeof partyFormSchema>;
