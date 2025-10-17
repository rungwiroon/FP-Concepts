# PowerShell script to test the Todo API

$baseUrl = "http://localhost:5000"

Write-Host "Testing Todo API..." -ForegroundColor Green
Write-Host ""

# Test 1: Create a todo
Write-Host "1. Creating a new todo..." -ForegroundColor Yellow
$createResponse = Invoke-RestMethod -Uri "$baseUrl/todos" -Method Post -ContentType "application/json" -Body '{"title":"Learn functional programming","description":"Study language-ext v5"}'
Write-Host "Created:" $createResponse | ConvertTo-Json
$todoId = $createResponse.id
Write-Host ""

# Test 2: List all todos
Write-Host "2. Listing all todos..." -ForegroundColor Yellow
$listResponse = Invoke-RestMethod -Uri "$baseUrl/todos" -Method Get
Write-Host "Todos:" $listResponse | ConvertTo-Json
Write-Host ""

# Test 3: Get specific todo
Write-Host "3. Getting todo by ID..." -ForegroundColor Yellow
$getResponse = Invoke-RestMethod -Uri "$baseUrl/todos/$todoId" -Method Get
Write-Host "Todo:" $getResponse | ConvertTo-Json
Write-Host ""

# Test 4: Update todo
Write-Host "4. Updating todo..." -ForegroundColor Yellow
$updateResponse = Invoke-RestMethod -Uri "$baseUrl/todos/$todoId" -Method Put -ContentType "application/json" -Body '{"title":"Learn functional programming with F#","description":"Study language-ext v5 and F#"}'
Write-Host "Updated:" $updateResponse | ConvertTo-Json
Write-Host ""

# Test 5: Toggle completion
Write-Host "5. Toggling completion status..." -ForegroundColor Yellow
$toggleResponse = Invoke-RestMethod -Uri "$baseUrl/todos/$todoId/toggle" -Method Patch
Write-Host "Toggled:" $toggleResponse | ConvertTo-Json
Write-Host ""

# Test 6: Create another todo
Write-Host "6. Creating another todo..." -ForegroundColor Yellow
$createResponse2 = Invoke-RestMethod -Uri "$baseUrl/todos" -Method Post -ContentType "application/json" -Body '{"title":"Build a web app","description":"Using ASP.NET Core"}'
Write-Host "Created:" $createResponse2 | ConvertTo-Json
$todoId2 = $createResponse2.id
Write-Host ""

# Test 7: List all todos again
Write-Host "7. Listing all todos again..." -ForegroundColor Yellow
$listResponse2 = Invoke-RestMethod -Uri "$baseUrl/todos" -Method Get
Write-Host "Todos:" $listResponse2 | ConvertTo-Json
Write-Host ""

# Test 8: Delete a todo
Write-Host "8. Deleting todo..." -ForegroundColor Yellow
Invoke-RestMethod -Uri "$baseUrl/todos/$todoId2" -Method Delete
Write-Host "Deleted todo $todoId2"
Write-Host ""

# Test 9: Final list
Write-Host "9. Final list of todos..." -ForegroundColor Yellow
$finalList = Invoke-RestMethod -Uri "$baseUrl/todos" -Method Get
Write-Host "Todos:" $finalList | ConvertTo-Json
Write-Host ""

Write-Host "All tests completed!" -ForegroundColor Green
