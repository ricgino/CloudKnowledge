export interface KnowledgeScopeState
{
  teamId: string | null;
  includeDescendants: boolean;
}

export type KnowledgeRetrievalScope =
  | {
      scope: 'all';
      teamId: null;
      includeDescendants: false;
    }
  | {
      scope: 'team';
      teamId: string;
      includeDescendants: boolean;
    };

export function initialKnowledgeScopeState():
  KnowledgeScopeState
{
  return {
    teamId: null,
    includeDescendants: false
  };
}

export function selectKnowledgeTeam(
  _state: KnowledgeScopeState,
  teamId: string):
  KnowledgeScopeState
{
  const selectedTeamId =
    teamId.trim();

  if (!selectedTeamId)
  {
    return initialKnowledgeScopeState();
  }

  return {
    teamId: selectedTeamId,
    includeDescendants: false
  };
}

export function selectAllKnowledge(
  _state: KnowledgeScopeState):
  KnowledgeScopeState
{
  return initialKnowledgeScopeState();
}

export function setKnowledgeDescendants(
  state: KnowledgeScopeState,
  includeDescendants: boolean):
  KnowledgeScopeState
{
  if (!state.teamId)
  {
    return initialKnowledgeScopeState();
  }

  return {
    teamId: state.teamId,
    includeDescendants
  };
}

export function toKnowledgeRetrievalScope(
  state: KnowledgeScopeState):
  KnowledgeRetrievalScope
{
  if (!state.teamId)
  {
    return {
      scope: 'all',
      teamId: null,
      includeDescendants: false
    };
  }

  return {
    scope: 'team',
    teamId: state.teamId,
    includeDescendants:
      state.includeDescendants
  };
}
