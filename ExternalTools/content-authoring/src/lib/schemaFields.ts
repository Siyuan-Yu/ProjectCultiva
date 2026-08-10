/** Field allow-lists aligned with Content/BaseGame/Data/SCHEMA.md + DefinitionSchema.cs */

export const TYPE_FIELDS: Record<string, Set<string>> = {
  character: new Set([
    'id', 'type', 'name', 'displayNameKey', 'nameKey', 'baseAttributes', 'tags',
    'personalityTags', 'backgroundTags', 'talentTags',
    'spiritRootPlaceholder', 'initialRealmPlaceholder'
  ]),
  cultivation: new Set([
    'id', 'type', 'name', 'displayNameKey', 'nameKey', 'requiredRealm',
    'cultivationSpeed', 'breakthroughProgress', 'grantedModifiers', 'tags'
  ]),
  item: new Set(['id', 'type', 'name', 'displayNameKey', 'nameKey', 'maxStack', 'tags']),
  opportunitySite: new Set([
    'id', 'type', 'name', 'nameKey', 'description', 'allowsCultivation', 'offeredManualId', 'tags'
  ]),
  openingScenario: new Set([
    'id', 'type', 'name', 'scheduleId', 'openingFactionId', 'openingSettlementId',
    'openingWorldRegionId', 'openingChapterId', 'spawns', 'openingRelations'
  ]),
  resource: new Set(['id', 'type', 'name', 'nameKey', 'tags']),
  facility: new Set([
    'id', 'type', 'name', 'laborResourceId', 'laborAmountPerWorker',
    'gatherResourceId', 'gatherAmountPerWorker', 'cultivateProgressBonusPerWorker', 'tags'
  ]),
  settlement: new Set(['id', 'type', 'name', 'initialStock', 'facilities']),
  worldRegion: new Set(['id', 'type', 'name', 'startLocationId', 'locations']),
  quest: new Set([
    'id', 'type', 'name', 'description', 'autoOffer',
    'offerConditions', 'completeConditions', 'failConditions', 'rewards', 'failResults'
  ]),
  contentEvent: new Set([
    'id', 'type', 'name', 'body', 'trigger', 'locationId', 'questId', 'once',
    'conditions', 'choices'
  ]),
  chapter: new Set([
    'id', 'type', 'name', 'description', 'openingScenarioId', 'plannedDays',
    'questChainIds', 'eventChainIds', 'dayBeats'
  ]),
  workArea: new Set([
    'id', 'type', 'name', 'locationId', 'tags', 'allowedActivities', 'offsetX', 'offsetZ'
  ]),
  job: new Set(['id', 'type', 'name', 'primaryWorkAreaId', 'activityBindings'])
};

export const LOCATION_FIELDS = new Set([
  'id', 'name', 'kind', 'adjacentIds', 'resourceOnExploreId', 'resourceOnExploreAmount',
  'opportunitySiteId', 'residentNpcDefinitionId', 'presentationX', 'presentationZ',
  'enterConditions', 'questOfferIds', 'tags', 'allowedActivities'
]);

export const CONDITION_KINDS = [
  'storyFlag', 'hasFlag', 'missingFlag', 'exploredLocation', 'atLocation',
  'stockAtLeast', 'realmAtLeast', 'hasManual', 'knowsSite', 'questActive',
  'questCompleted', 'exploredLocation'
];

export const OUTCOME_KINDS = [
  'setFlag', 'clearFlag', 'addStock', 'grantProgress', 'discoverSite',
  'relationDelta', 'startQuest'
];

export const EVENT_TRIGGERS = [
  'manual', 'onArrive', 'onExplore', 'onQuestCompleted', 'onQuestFailed'
];

export const SCHEDULE_ACTIVITIES = [
  'Labor', 'Rest', 'Eat', 'Cultivate', 'Explore', 'Patrol', 'Inspect'
];
