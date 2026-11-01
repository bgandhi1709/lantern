import { z } from 'zod';

import { api } from '../shared/api';

const Challenge = z.object({ challenge: z.string() });
const Proof = z.object({ proof: z.string() });

export type OneTimeCodeOperation = 'delete-child';

export const requestCode = async (operation: OneTimeCodeOperation, target: string) =>
  (await api.post('/v1/security/code', Challenge, { operation, target })).challenge;

export const verifyCode = async (challenge: string, code: string) =>
  (await api.post('/v1/security/code/verify', Proof, { challenge, code })).proof;
