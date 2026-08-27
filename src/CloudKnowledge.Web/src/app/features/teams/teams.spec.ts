import {
  buildTeamTreeRows,
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
});
