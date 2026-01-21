# Similar Data Objects Implementation

## Overview
Implemented `GetSimilarDataObjects()` method in [DataObjects.cs](hasheous-lib/Classes/DataObjects.cs#L3206) to find the top 10 most similar DataObjects based on tag comparisons.

## Algorithm

### Input Validation
1. Validates that the input DataObject is non-null and has attributes
2. Extracts the Tags attribute from the DataObject
3. Returns an empty list if the source object has no tags

### Tag Extraction & Indexing
1. Casts the tag attribute value to `Dictionary<DataObjectItemTags.TagType, DataObjectItemTags>`
2. Builds an indexed dictionary of source tag IDs grouped by `TagType` for O(1) lookups
3. Structure: `Dictionary<TagType, HashSet<long>>` where each TagType maps to its tag IDs

### Candidate Collection
1. Queries database for all DataObjects of the same type (excluding the source object)
2. Retrieves candidate IDs to minimize memory footprint before similarity calculations

### Per-Category Similarity Calculation
For each candidate DataObject:

1. **Fetch candidate tags** using `GetTags(candidateId)`
2. **For each tag category** in the source object:
   - If candidate has tags in that category:
     - Count intersecting tags: `sourceTagIds ∩ candidateTagIds`
     - Calculate category similarity: `(matchCount / sourceTagIds.Count) × 100%`
   - If candidate lacks tags in that category: 0% similarity for that category
3. **Store per-category scores** with keys like `"GameGenre"`, `"GameGameplay"`, etc.

### Overall Similarity Scoring
$$\text{Overall Similarity} = \frac{\sum \text{Category Similarities}}{\text{Number of Categories with Source Tags}}$$

- Averages the per-category similarities across all categories that have source tags
- Only includes categories where the source object has at least one tag
- Filters out candidates with 0% overall similarity before ranking

### Ranking & Result Building
1. Sorts candidates by overall similarity in descending order
2. Takes the top 10 candidates
3. Fetches full DataObject details for each top candidate
4. Returns a `DataObjectsList` with:
   - Up to 10 objects
   - Sorted by overall similarity (highest first)
   - Per-category similarity scores embedded in JSON format
   - Count, PageNumber, PageSize, and TotalPages metadata

## Key Features

### Category-Specific Insights
- Tracks similarity for each tag type independently
- Allows UI to display results like:
  - "88% similar Gameplay"
  - "75% similar Themes"
  - "45% Overall Similarity"

### Performance Considerations
- Uses `HashSet<long>` for tag ID lookups: O(1) per comparison
- Lazy loads candidate tags only when needed
- Filters out zero-similarity candidates before fetching full objects
- Returns at most 10 results to limit payload

### Flexibility
- Works with any DataObjectType that supports tags (Game, Platform)
- Reuses existing `GetTags()` and `GetDataObject()` methods
- No new database schema required

## Example Calculation

**Source Game: "Dark Souls"**
- Tags:
  - **Genre**: Action, RPG, Fantasy
  - **Gameplay**: Single-Player, Boss-Battles
  - **Theme**: Dark, Medieval

**Candidate Game: "Elden Ring"**
- Tags:
  - **Genre**: Action, RPG, Medieval ← (2/3 match = 67%)
  - **Gameplay**: Single-Player, Exploration ← (1/2 match = 50%)
  - **Theme**: Dark, Gothic ← (1/3 match = 33%)

**Similarity Scores:**
- Genre: 67%
- Gameplay: 50%
- Theme: 33%
- **Overall: (67 + 50 + 33) / 3 = 50%**

## Extension Points

Future enhancements could include:
- **Weighted categories**: Give more importance to certain tag types
- **AI-generated tag filtering**: Option to exclude or weight AI-generated tags differently
- **Fuzzy matching**: Semantic similarity between tag names
- **Pagination**: Support for returning results in batches beyond top 10
- **Custom weighting**: Allow consumers to specify which tag categories matter most

## Database Queries

- **Candidate enumeration**: `SELECT Id FROM DataObject WHERE ObjectType = ? AND Id != ?`
- **Tag fetching**: Leverages existing `GetTags()` which uses:
  ```sql
  SELECT Tags.id, Tags.type, Tags.name, DataObject_Tags.AIAssigned 
  FROM Tags 
  INNER JOIN DataObject_Tags ON Tags.id = DataObject_Tags.TagId 
  WHERE DataObject_Tags.DataObjectId = ?
  ```

## Testing Recommendations

1. **Zero tags**: DataObject with no tags returns empty list
2. **No candidates**: Only DataObject of its type returns empty list
3. **Single match**: One candidate with identical tags shows 100% similarity
4. **Partial overlap**: Multiple candidates ranked correctly by similarity percentage
5. **Cross-category**: Different tag types contribute equally to overall score
6. **Ordering**: Results always sorted by overall similarity descending
