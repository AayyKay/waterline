const test = require('node:test');
const assert = require('node:assert/strict');
const { getReminderState } = require('./reminders.cjs');

const settings = { goal: 80, interval: 60, start: '09:00', end: '17:00', reminders: true, weekdays: [1, 2, 3, 4, 5] };
const localDate = (year, month, day, hour, minute) => new Date(year, month - 1, day, hour, minute);

test('schedules from workday start before work begins', () => {
  const result = getReminderState({ now: localDate(2026, 9, 2, 8, 30), settings, total: 0 });
  assert.equal(result.dueAt.getHours(), 10);
  assert.equal(result.active, false);
});

test('logging a drink resets the reminder interval', () => {
  const lastDrinkAt = localDate(2026, 9, 2, 11, 15).toISOString();
  const result = getReminderState({ now: localDate(2026, 9, 2, 11, 20), settings, total: 12, lastDrinkAt });
  assert.equal(result.dueAt.getHours(), 12);
  assert.equal(result.dueAt.getMinutes(), 15);
});

test('stops reminders when the goal is reached', () => {
  const result = getReminderState({ now: localDate(2026, 9, 2, 12, 0), settings, total: 80 });
  assert.equal(result.dueAt, null);
});

test('moves an after-hours reminder to the next enabled day', () => {
  const result = getReminderState({ now: localDate(2026, 9, 4, 18, 0), settings, total: 12 });
  assert.equal(result.dueAt.getDay(), 1);
  assert.equal(result.dueAt.getHours(), 10);
});

test('a sent notification advances the next reminder', () => {
  const lastNotificationAt = localDate(2026, 9, 2, 12, 0).toISOString();
  const result = getReminderState({ now: localDate(2026, 9, 2, 12, 1), settings, total: 12, lastNotificationAt });
  assert.equal(result.dueAt.getHours(), 13);
});
