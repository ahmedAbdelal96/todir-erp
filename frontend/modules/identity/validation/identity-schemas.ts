import { z } from "zod";

export const createUserSchema = z.object({
  name: z
    .string()
    .min(1, "يرجى إدخال اسم المستخدم")
    .max(200, "يجب ألا يتجاوز الاسم 200 حرف"),
  emailPrefix: z
    .string()
    .min(1, "يرجى إدخال الجزء الأول من البريد الإلكتروني")
    .max(64, "يجب ألا يتجاوز 64 حرفًا")
    .regex(/^[a-zA-Z0-9._-]+$/, "يجب أن يحتوي البريد على حروف لاتينية وأرقام ونقاط فقط بدون مسافات أو رموز خاصة"),
  roleIds: z.array(z.string().uuid("معرّف الدور غير صالح")).optional().default([])
});

export type CreateUserFormValues = z.infer<typeof createUserSchema>;

export const changeLoginDomainSchema = z.object({
  loginDomain: z
    .string()
    .min(3, "يجب ألا يقل النطاق عن 3 أحرف")
    .max(253, "يجب ألا يتجاوز النطاق 253 حرفًا")
    .regex(
      /^(?:[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,}$/,
      "يرجى إدخال نطاق صالح (مثال: company.com)"
    )
});

export type ChangeLoginDomainFormValues = z.infer<typeof changeLoginDomainSchema>;
