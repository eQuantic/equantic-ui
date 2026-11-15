## ADDED Requirements

### Requirement: A collection's negative count is refused with its value

Where .NET refuses a negative count with `ArgumentOutOfRangeException.ThrowIfNegative` (`Queue<T>`'s and `Stack<T>`'s capacity in the constructor, `EnsureCapacity` and `TrimExcess`, `LinkedList<T>.CopyTo`'s index, `HashSet<T>.CopyTo`'s index and count), the runtime SHALL throw the same exception with its `ParamName`, its `ActualValue` and its message, the value written as the current culture writes an `int`.

#### Scenario: A queue built with a negative capacity

- **WHEN** `new Queue<int>(-1)` runs and the exception is caught
- **THEN** its `ParamName` is `capacity`, its `ActualValue` is -1 and its message says so, as in .NET; `ActualValue` was null

#### Scenario: In a culture with its own minus sign

- **WHEN** `new Queue<int>(-1)` runs in sv-SE
- **THEN** the message and `ActualValue`'s text write the value with sv-SE's minus sign, as .NET does
