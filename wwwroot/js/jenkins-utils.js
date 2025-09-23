// Utility functions for Jenkins functionality

function showLoading(elementId, message) {
    const element = document.getElementById(elementId);
    if (element) {
        element.innerHTML = `<option value="">${message}</option>`;
        element.disabled = true;
        console.log(`showLoading: Disabled ${elementId} with message: ${message}`);
    }
}

function showError(elementId, message) {
    const element = document.getElementById(elementId);
    if (element) {
        element.innerHTML = `<option value="" style="color: red;">${message}</option>`;
        element.disabled = false;
        console.log(`showError: Enabled ${elementId} with error: ${message}`);
    }
}