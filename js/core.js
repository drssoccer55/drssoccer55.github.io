/*
 * This file contains the core objects and functions used to run the motivity web app
 */

/*
 * Create a counter which can increment, decrement, and return current value. Optionally can be created with text field
 * to update after increment/decrement.
 */
function Counter(counterDisplay) {
    var counter = 0;

    function increment() {
        counter = counter + 1;
        refreshCounterDisplay();
    };

    function decrement() {
        counter = counter - 1;
        refreshCounterDisplay();
    };

    function getValue() {
        return counter;
    };

    function refreshCounterDisplay() {
        if (counterDisplay !== undefined) {
            counterDisplay.innerHTML = counter;
        }
    }

    return {
        increment : increment,
        decrement : decrement,
        getValue : getValue
    };
}

/*
 * Create a lock which can be toggled and the current status can be returned. Optionally cam be created with button and
 * style classes used to display.
 */
function Lock(displayButton, lockedStyle, unlockedStyle) {
    var lockVal = true;

    function toggleLock() {
        lockVal = !lockVal;
        refreshLockDisplay();
    };

    function isLocked() {
        return lockVal;
    };

    function refreshLockDisplay() {
        if (displayButton !== undefined && lockedStyle !== undefined && unlockedStyle !== undefined) {
            if (lockVal) {
                displayButton.className = lockedStyle;
            } else {
                displayButton.className = unlockedStyle;
            }
        }
    }

     return {
        toggleLock : toggleLock,
        isLocked : isLocked
    };
}

/*
 * Utility function to only execute the given function if unlocked. Otherwise do nothing.
 */
function executeFunctionIfUnlocked(fun, lock) {
    if (!lock.isLocked()) {
        fun();
    }
}

/*
 * Creates a barCounterControl object which has access to both the bar counter and the page lock.
 */
function BarCounterControl(barCounter, interactivityLock) {
    function increment() {
        executeFunctionIfUnlocked(barCounter.increment, interactivityLock);
    }

    function decrement() {
        executeFunctionIfUnlocked(barCounter.decrement, interactivityLock);
    }

    return {
        increment : increment,
        decrement : decrement
    };
}

/*
 * Creates a fooIncrementer object which has access to both the foo counter, bar counter, and the page lock.
*/
function FooCounterControl(fooCounter, barCounter, fooCounterLimit, interactivityLock) {
    function increment() {
        executeFunctionIfUnlocked(getCounterToModify().increment, interactivityLock);
    }

    function decrement() {
        executeFunctionIfUnlocked(getCounterToModify().decrement, interactivityLock);
    }

    /*
     * Returns true if limit does not exist or below limit. I am interpreting the instructions as the below limit is
     * before the operation takes place and after the operation it may not be below the limit anymore.
     */
    function isBelowLimit() {
        var limit = parseInt(fooCounterLimit.value);
        if (!isNaN(limit)) {
            return (fooCounter.getValue() <= limit);
        } else {
            return true;
        }
    }

    // Returns the counter that should be modified
    function getCounterToModify() {
        if (isBelowLimit()) {
            return fooCounter;
        } else {
            return barCounter;
        }
    }

    return {
        increment : increment,
        decrement : decrement
    };
}

/*
 * Creates a search control object which has access to the term and searchable text values and will update the
 * search output display when the search function is operated and the page is not locked.
 */
function SearchControl(searchTermField, searchableTextField, searchOutput, interactivityLock) {
    function performSearch() {
        executeFunctionIfUnlocked(search, interactivityLock);
    }

    /*
     * Sets searchOutput to a string of comma separated offsets of occurrences of the search term in the searchable
     * text. Explicitly returns sets output if no matches are found or one or more input fields are empty.
     */
    function search() {
        if (isEmptyTextField(searchTermField)) {
            if (isEmptyTextField(searchableTextField)) {
                searchOutput.innerHTML = "Both search term and searchable text fields are empty";
            } else {
                searchOutput.innerHTML = "Search term field is empty";
            }
        } else if (isEmptyTextField(searchableTextField)) {
            searchOutput.innerHTML = "Searchable text field is empty";
        } else {
            searchOutput.innerHTML = getOffsetDisplayString(getSearchTermOffsets(searchableTextField.value, searchTermField.value));
        }
    }

    /*
     * Get the array of offsets. This function is case sensitive!
     */
    function getSearchTermOffsets(searchableText, searchTerm) {
        var offsets = [];
        var offset = searchableText.indexOf(searchTerm);
        while(offset != -1) {
            offsets.push(offset);
            offset = searchableText.indexOf(searchTerm, offset + 1);
        }
        return offsets;
    }

    /*
     * Returns the display text from offset array
     */
    function getOffsetDisplayString(offsetArray) {
        var length = offsetArray.length;
        var text = "";
        if (length === 0) {
            text = "No matches";
        } else {
            var i;
            for (i = 0; i < length - 1; i++) {
                text += offsetArray[i].toString() + ",";
            }
            // Last one shouldn't have a comma after
            text += offsetArray[length - 1].toString();
        }

        return text;
    }

    return {
        performSearch : performSearch
    };
}