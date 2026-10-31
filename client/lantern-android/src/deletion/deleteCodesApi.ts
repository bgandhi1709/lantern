import { z } from 'zod';

import { api } from '../shared/api';

const Proof = z.object({ proof: z.string() });

export type DeleteScope = { scope: 'child'; childId: string };

export const requestDeleteCode = (scope: DeleteScope) =>
  api.post('/v1/family/delete-codes', z.unknown(), scope);

export const verifyDeleteCode = async (code: string) =>
  (await api.post('/v1/family/delete-codes/verify', Proof, { code })).proof;

export const deleteChildWithProof = (childId: string, proof: string) =>
  api.delete(`/v1/family/children/${childId}`, z.unknown(), { 'Lantern-Delete-Proof': proof });
