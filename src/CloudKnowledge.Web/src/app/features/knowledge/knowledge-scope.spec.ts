import {
  initialKnowledgeScopeState,
  selectAllKnowledge,
  selectKnowledgeTeam,
  setKnowledgeDescendants,
  toKnowledgeRetrievalScope
} from './knowledge-scope';

describe('knowledge retrieval scope', () => {
  it('starts with all accessible knowledge', () => {
    expect(initialKnowledgeScopeState())
      .toEqual({
        teamId: null,
        includeDescendants: false
      });

    expect(
      toKnowledgeRetrievalScope(
        initialKnowledgeScopeState()))
      .toEqual({
        scope: 'all',
        teamId: null,
        includeDescendants: false
      });
  });

  it('selects an exact team by default and can include descendants', () => {
    const exactTeam =
      selectKnowledgeTeam(
        initialKnowledgeScopeState(),
        'desk-sharing');

    expect(exactTeam)
      .toEqual({
        teamId: 'desk-sharing',
        includeDescendants: false
      });

    const branch =
      setKnowledgeDescendants(
        exactTeam,
        true);

    expect(
      toKnowledgeRetrievalScope(branch))
      .toEqual({
        scope: 'team',
        teamId: 'desk-sharing',
        includeDescendants: true
      });
  });

  it('resets descendants when switching teams or returning to all', () => {
    const branch =
      setKnowledgeDescendants(
        selectKnowledgeTeam(
          initialKnowledgeScopeState(),
          'rai'),
        true);

    expect(
      selectKnowledgeTeam(
        branch,
        'hr-portal'))
      .toEqual({
        teamId: 'hr-portal',
        includeDescendants: false
      });

    expect(
      selectAllKnowledge(branch))
      .toEqual({
        teamId: null,
        includeDescendants: false
      });
  });

  it('ignores descendant toggles when no team is selected', () => {
    expect(
      setKnowledgeDescendants(
        initialKnowledgeScopeState(),
        true))
      .toEqual({
        teamId: null,
        includeDescendants: false
      });
  });
});
