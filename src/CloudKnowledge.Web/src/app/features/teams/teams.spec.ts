import {
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

  it('allows team deletion only for direct owners', () => {
    const baseTeam: TeamItem = {
      id: 'team',
      name: 'Team',
      parentTeamId: null,
      isMember: true,
      role: 'Owner',
      canManage: true
    };

    expect(canDeleteTeam(baseTeam)).toBeTrue();

    expect(canDeleteTeam({
      ...baseTeam,
      role: 'Admin'
    })).toBeFalse();

    expect(canDeleteTeam({
      ...baseTeam,
      role: 'Member',
      canManage: false
    })).toBeFalse();

    expect(canDeleteTeam({
      ...baseTeam,
      isMember: false,
      role: null,
      canManage: false
    })).toBeFalse();
  });
});
