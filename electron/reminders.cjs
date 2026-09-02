function atLocalTime(date, value) {
  const [hours, minutes] = value.split(':').map(Number);
  const result = new Date(date);
  result.setHours(hours, minutes, 0, 0);
  return result;
}

function nextEnabledDayStart(from, settings) {
  for (let offset = 0; offset <= 7; offset += 1) {
    const candidate = new Date(from);
    candidate.setDate(candidate.getDate() + offset);
    const start = atLocalTime(candidate, settings.start);
    if (settings.weekdays.includes(start.getDay()) && (offset > 0 || start > from)) return start;
  }
  return null;
}

function getReminderState({ now, settings, total, lastDrinkAt, lastNotificationAt }) {
  if (!settings.reminders || total >= settings.goal || settings.weekdays.length === 0) {
    return { dueAt: null, active: false };
  }

  const start = atLocalTime(now, settings.start);
  const end = atLocalTime(now, settings.end);
  const isEnabledDay = settings.weekdays.includes(now.getDay());
  const isActive = isEnabledDay && now >= start && now <= end;
  const intervalMs = settings.interval * 60_000;

  if (!isEnabledDay || now > end) {
    const nextStart = nextEnabledDayStart(now, settings);
    return { dueAt: nextStart ? new Date(nextStart.getTime() + intervalMs) : null, active: false };
  }

  if (now < start) return { dueAt: new Date(start.getTime() + intervalMs), active: false };

  const anchors = [start.getTime()];
  if (lastDrinkAt) anchors.push(new Date(lastDrinkAt).getTime());
  if (lastNotificationAt) anchors.push(new Date(lastNotificationAt).getTime());
  const dueAt = new Date(Math.max(...anchors) + intervalMs);
  return { dueAt: dueAt <= end ? dueAt : null, active: isActive };
}

module.exports = { atLocalTime, getReminderState, nextEnabledDayStart };
