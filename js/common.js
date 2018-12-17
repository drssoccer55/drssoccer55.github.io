/*
 * This file contains generic common utility functions
 */

/*
 * Checks if a text field is empty
 */
function isEmptyTextField(textField) {
    return (!textField.value);
}

/*
 * Creates an html string multiplicationTable of size maxInteger. All pairs of positive integers less than or equal to
 * maxInteger are computed. Each line is grouped by the lesser of the two factors and the products are space separated.
 * The lines are sorted ascending by the grouped factor. A pair may contain the same integer.
 */
function multiplicationTable(maxInteger) {
    var text = "";
    var i;
    for (i = 1; i <= maxInteger; i++) {
        var j;
        for (j = i; j <= maxInteger; j++) {
            text += i + "x" + j + "=" + i*j;
            if (j !== maxInteger) {
                text += " ";
            }
        }
        if (i !== maxInteger) {
            text += "<br></br>";
        }
    }
    return text;
}