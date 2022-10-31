const mainWindow = document.getElementById("window");
const popupWindow = document.getElementById("popup");


function OpenMeaningOfLife() {
    mainWindow.style.opacity = "0.3";
    popupWindow.style.opacity = "1";
}


function CloseMeaningOfLife() {
    mainWindow.style.opacity = "1";
    popupWindow.style.opacity = "0";
}