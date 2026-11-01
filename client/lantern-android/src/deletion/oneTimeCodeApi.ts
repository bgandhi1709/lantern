import { z } from 'zod';

import { api } from '../shared/api';

const Challenge = z.object({ challenge: z.string() });
const Proof = z.object({ proof: z.string() });

export type CodeOperation = { operation: string; target?: string };

export const requestCode = async (request: CodeOperation) =>
  (await api.post('/v1/security/code', Challenge, request)).challenge;

export const verifyCode = async (challenge: string, code: string) =>
  (await api.post('/v1/security/code/verify', Proof, { challenge, code })).proof;

export const deleteChildWithProof = (childId: string, proof: string) =>
  api.delete(`/v1/family/children/${childId}`, z.unknown(), { 'Lantern-Code-Proof': proof });
