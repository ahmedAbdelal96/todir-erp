// Platform importing module internal directly (forbidden)
import { internalA } from "../modules/fixture-a/internal";

export const platformService = () => internalA();
