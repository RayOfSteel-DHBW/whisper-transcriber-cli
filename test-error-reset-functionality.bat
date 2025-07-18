@echo off
echo Testing Error Reset Functionality
echo ================================
echo.
echo This test verifies that failed tasks can be properly reset to pending status.
echo.
echo Instructions:
echo 1. Run the TaskUI application
echo 2. Add a file that will fail (e.g., a non-audio file with audio extension)
echo 3. Start the queue - the task should fail and show "Error" status
echo 4. Try one of these reset methods:
echo    Method 1: Click "Reset Errors" button
echo    Method 2: Stop the queue, then click "Start Queue" and choose "Reset & Start"
echo 5. Verify the failed task now shows "Pending" status
echo 6. The task should be processable again (if the underlying issue is fixed)
echo.
echo Expected behavior:
echo - Error tasks change from "Error" to "Pending" status
echo - Error messages are cleared
echo - Progress resets to 0%
echo - Tasks can be reprocessed when queue is started
echo.
echo Additional test:
echo - Tasks that were stopped mid-processing should also reset to "Pending"
echo - No tasks should remain in "Error" status after reset operations
echo.
pause