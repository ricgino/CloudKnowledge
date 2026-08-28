import {
  buildKnowledgeTeamOptions,
  buildTeamTreeRows,
  canDeleteTeam,
  TeamItem
} from './teams';

describe('team tree', () => {
  it('builds structural ancestors before direct memberships at arbitrary depth', () => {
    const stellantis: TeamItem = {
      id: 'stellantis',
      name: 'Stellantis',
      parentTeamId: null,
      isMember: false,
      role: null,
      canManage: false
    };

    const finance: TeamItem = {
      id: 'finance',
      name: 'Finance',
      parentTeamId: 'stellantis',
      isMember: false,
      role: null,
      canManage: false
    };

    const reporting: TeamItem = {
      id: 'reporting',
      name: 'Reporting',
      parentTeamId: 'finance',
      isMember: true,
      role: 'Member',
      canManage: false
    };

    const rows =
      buildTeamTreeRows([
        reporting,
        stellantis,
        finance
      ]);

    expect(
      rows.map(row => [
        row.id,
        row.depth,
        row.isMember
      ]))
      .toEqual([
        ['stellantis', 0, false],
        ['finance', 1, false],
        ['reporting', 2, true]
      ]);
  });

  it('builds unambiguous full paths for knowledge scope options', () => {
    const teams: TeamItem[] = [
      {
        id: 'rai',
        name: 'Rai',
        parentTeamId: null,
        isMember: false,
        role: null,
        canManage: false
      },
      {
        id: 'desk-sharing',
        name: 'DeskSharing',
        parentTeamId: 'rai',
        isMember: true,
        role: 'Member',
        canManage: false
      },
      {
        id: 'stellantis',
        name: 'Stellantis',
        parentTeamId: null,
        isMember: false,
        role: null,
        canManage: false
      },
      {
        id: 'finance',
        name: 'Finance',
        parentTeamId: 'stellantis',
        isMember: false,
        role: null,
        canManage: false
      },
      {
        id: 'reporting',
        name: 'Reporting',
        parentTeamId: 'finance',
        isMember: true,
        role: 'Member',
        canManage: false
      }
    ];

    expect(
      buildKnowledgeTeamOptions(teams))
      .toEqual([
        {
          id: 'rai',
          label: 'Rai'
        },
        {
          id: 'desk-sharing',
          label: 'Rai / DeskSharing'
        },
        {
          id: 'stellantis',
          label: 'Stellantis'
        },
        {
          id: 'finance',
          label: 'Stellantis / Finance'
        },
        {
          id: 'reporting',
          label: 'Stellantis / Finance / Reporting'
        }
      ]);
  });

  it('keeps malformed orphan and cyclic teams usable without recursion loops', () => {
    const malformed: TeamItem[] = [
      {
        id: 'orphan',
        name: 'Orphan',
        parentTeamId: 'missing',
        isMember: true,
        role: 'Member',
        canManage: false
      },
      {
        id: 'cycle-a',
        name: 'Cycle A',
        parentTeamId: 'cycle-b',
        isMember: false,
        role: null,
        canManage: false
      },
      {
        id: 'cycle-b',
        name: 'Cycle B',
        parentTeamId: 'cycle-a',
        isMember: true,
        role: 'Member',
        canManage: false
      }
    ];

    const options =
      buildKnowledgeTeamOptions(malformed);

    expect(options).toHaveLength(3);
    expect(options.find(option => option.id === 'orphan')?.label)
      .toBe('Orphan');
    expect(options.every(option => option.label.length > 0))
      .toBe(true);
  });

  it('allows team deletion only for direct owners', () => {
    const baseTeam: TeamItem = {
      id: 'team',
      name: 'Team',
      parentTeamId: null,
      isMember: true,
      role: 'Owner',
      canManage: true
    };

    expect(canDeleteTeam(baseTeam)).toBe(true);

    expect(canDeleteTeam({
      ...baseTeam,
      role: 'Admin'
    })).toBe(false);

    expect(canDeleteTeam({
      ...baseTeam,
      role: 'Member',
      canManage: false
    })).toBe(false);

    expect(canDeleteTeam({
      ...baseTeam,
      isMember: false,
      role: null,
      canManage: false
    })).toBe(false);
  });
});
