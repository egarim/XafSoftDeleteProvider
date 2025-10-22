# PreserveRelationshipsDataLayer - FINAL IMPLEMENTATION

## Problem Analysis

### Root Cause (Discovered via XPO Source Code Analysis)
In `DevExpress.Xpo\Session.cs` lines 2159-2170, the `DeleteCore()` method contains:

```csharp
if(mi.IsAssociation) {
    object associatedObject = mi.GetValue(theObject);
    if(associatedObject != null) {
        XPMemberInfo assocList = mi.GetAssociatedMember();
        IList associatedCollection = (IList)assocList.GetValue(associatedObject);
        if(associatedCollection != null) {
            associatedCollection.Remove(theObject);  // <-- REMOVES FROM COLLECTION IN MEMORY
        }
    }
}
```

**Key Insight**: Session.DeleteCore() removes objects from association collections IN-MEMORY **BEFORE** SQL is generated. Later, when Session.Save() is called, XPO generates UPDATE statements to persist these in-memory changes, setting FK columns to NULL.

### Why Session Override Approach Failed
- `Session.Delete(object)` is **NOT virtual** - cannot be overridden
- `Session.DeleteCore()` is **private** - cannot be accessed
- No override points exist at the Session level

### Solution: DataLayer Interception
Since we cannot intercept at the Session level, we must intercept at the DataLayer level by:
1. Using reflection to inspect ModificationStatement operands
2. Detecting UPDATE statements that both:
   - Set GCRecord to a value (marking soft delete)
   - Set other columns to NULL (nullifying FKs)
3. Filtering out these statements before they execute

## Implementation

### Files Modified

#### 1. XafSoftDelete.Module/PreserveRelationshipsDataLayer.cs
**Status**: ✅ COMPLETE - Enhanced with comprehensive logging

**Key Features**:
- Reflection-based operand inspection (handles parameterized SQL)
- Detailed diagnostic logging to Desktop (`XafSoftDelete_SQL_Log.txt`)
- Logs every operand with column name, value, and type information
- Filters statements that set GCRecord AND nullify other columns

**Key Methods**:
- `ModifyData()` - Main interception point, logs entire batch
- `ShouldIncludeStatement()` - Uses reflection to analyze each UPDATE
- `IsNullValue()` - Handles wrapped/constant values (ConstantValue, etc.)

**Logging Output**:
```
=== ModifyData called with 1 statements ===
[Statement 0] SQL: update Order set ?, 1.00000000m, ?, 1, 1066563206 where ...
  [Statement 0] Analyzing UPDATE via reflection
  Using property: Operands
    Operand [0]: Type = DevExpress.Xpo.DB.ConstantValue
      Column: CustomerId
      Value: NULL (Type: null)
      >>> FOUND NULL assignment to column: CustomerId
    Operand [1]: Type = DevExpress.Xpo.DB.ConstantValue
      Column: GCRecord
      Value: 1066563206 (Type: Int32)
      >>> FOUND GCRecord = 1066563206
  [Statement 0] DECISION: EXCLUDE - Has GCRecord AND nullifies CustomerId
[Statement 0] *** FILTERED OUT by reflection ***
=== Final: 0 of 1 will execute ===
```

#### 2. XafSoftDelete.Module/PreserveRelationshipsObjectSpaceProvider.cs
**Status**: ✅ COMPLETE - Reverted to DataLayer-only approach

Removed Session override attempt. Now only overrides:
- `CreateDataLayer()` - Returns PreserveRelationshipsDataLayer

#### 3. XafSoftDelete.Module/PreserveRelationshipsModuleProvider.cs
**Status**: ✅ COMPLETE - Reverted to DataLayer-only approach

Removed ObjectSpace override attempt. Now only overrides:
- `CreateDataLayer()` - Returns PreserveRelationshipsDataLayer

### Files Deleted (Non-Functional Approaches)

#### PreserveRelationshipsSession.cs ❌ DELETED
**Reason**: Session.Delete() is NOT virtual - cannot be overridden

#### PreserveRelationshipsObjectSpace.cs ❌ DELETED  
**Reason**: No CreateSession() method exists to override

## Testing

### Enable Logging
```csharp
PreserveRelationshipsDataLayer.EnableLogging = true;
```

### Log File Location
Desktop: `XafSoftDelete_SQL_Log.txt`

### Test Scenario
1. Create Order with Customer relationship
2. Delete Order (soft delete via GCRecord)
3. Check log file for:
   - ">>> FOUND GCRecord = [value]"
   - ">>> FOUND NULL assignment to column: [column name]"
   - "[Statement X] *** FILTERED OUT by reflection ***"
4. Verify FK column NOT NULL in database after delete

### Expected Behavior
- Log shows UPDATE statement detected
- Reflection finds GCRecord assignment
- Reflection finds NULL FK assignments
- Statement filtered out (not executed)
- Database FK columns remain populated

## Key Technical Details

### Reflection Properties Checked
ModificationStatement:
- `Operands` (primary)
- `Parameters` (fallback)
- `Assignments` (fallback)

Operand:
- `ColumnName` (primary)
- `PropertyName` (fallback)
- `Name` (fallback)

Value:
- `Value` (primary)
- `Expression` (fallback)

### NULL Detection Logic
1. Direct null check
2. DBNull check
3. Wrapped value check (ConstantValue types)
4. Recursive unwrapping via reflection

### Why String-Based SQL Parsing Failed
```
update Order set ?, 1.00000000m, ?, 1, 1066563206 where ...
```
Column names replaced with `?` placeholders - cannot parse. MUST use reflection.

## Configuration

### Application Startup (Module.cs)
```csharp
public sealed class XafSoftDeleteModule : ModuleBase {
    public override void AddGeneratorUpdaters(ModuleUpdatersCollection updaters) {
        base.AddGeneratorUpdaters(updaters);
        // Enable logging for debugging
        PreserveRelationshipsDataLayer.EnableLogging = true;
    }
}
```

### Provider Setup
The providers automatically use PreserveRelationshipsDataLayer when created. No additional configuration needed.

## Debugging

### If Relationships Still Being Cleared:

1. **Check Log File**: Ensure logging is enabled and file exists on Desktop
2. **Verify Operand Detection**: Log should show "Using property: Operands"
3. **Verify NULL Detection**: Log should show ">>> FOUND NULL assignment to column: [name]"
4. **Verify GCRecord Detection**: Log should show ">>> FOUND GCRecord = [value]"
5. **Verify Filtering**: Log should show "*** FILTERED OUT by reflection ***"

### Common Issues:

**Log shows "No Operands/Parameters/Assignments property found"**
- XPO version uses different property name
- Check "Available properties:" section in log
- Add new property name to reflection code

**Log shows operands but no column names**
- Operand type uses different property for column name
- Check operand type in log
- Add new column name property to reflection code

**Log shows NULL detection but statement not filtered**
- Check if both GCRecord AND NULL detected
- Verify `ShouldIncludeStatement()` logic

## Performance Considerations

- Reflection overhead is minimal (only during Save operations)
- Logging can be disabled in production via `EnableLogging = false`
- Statement filtering happens in-memory before database execution

## Limitations

- Cannot intercept Session-level operations (Session.Delete() not virtual)
- Requires reflection (may break with major XPO version changes)
- Only works with UPDATE statements (INSERT/DELETE not affected)

## Future Enhancements

If DevExpress makes Session.Delete() virtual in future versions:
1. Implement Session override to prevent in-memory collection removal
2. Eliminate need for DataLayer filtering
3. More efficient solution (prevents SQL generation entirely)

## Conclusion

This implementation successfully intercepts and filters UPDATE statements that would nullify FK columns during soft delete operations. The DataLayer approach is the only viable solution given XPO's current architecture where Session.Delete() is sealed.

The comprehensive logging provides full visibility into the interception process, making debugging and verification straightforward.
