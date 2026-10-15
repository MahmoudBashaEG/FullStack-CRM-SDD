/** Matches Crm.Api AgentResponse. */
export interface Agent {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber: string | null;
  isActive: boolean;
  createdAtUtc: string;
}

export interface CreateAgentRequest {
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string | null;
  password: string;
}

export interface UpdateAgentRequest {
  firstName: string;
  lastName: string;
  phoneNumber: string | null;
}
