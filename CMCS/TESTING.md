# CMCS Testing Checklist

## Lecturer Functionality
- [ ] Login as lecturer
- [ ] View dashboard with stats
- [ ] Submit new claim with valid data
- [ ] Submit claim with file upload
- [ ] Auto-calculation of total amount works
- [ ] Save claim as draft
- [ ] View claim details
- [ ] View claim history
- [ ] Filter claims in history

## Coordinator Functionality
- [ ] Login as coordinator
- [ ] View dashboard with pending claims
- [ ] Filter claims (All, Urgent, This Week, By Module)
- [ ] Approve individual claim
- [ ] Reject individual claim with reason
- [ ] Bulk approve multiple claims
- [ ] Bulk reject multiple claims with reason
- [ ] View claim details

## Module Management
- [ ] View all modules
- [ ] Create new module
- [ ] Edit module details
- [ ] Delete module (with no claims)
- [ ] Toggle module active/inactive status
- [ ] Validate duplicate module codes

## File Upload
- [ ] Upload valid file types
- [ ] Reject invalid file types
- [ ] Reject files over 10MB
- [ ] Drag and drop functionality
- [ ] View uploaded files in claim details
- [ ] Download uploaded files

## Error Handling
- [ ] Invalid login shows error
- [ ] Invalid form submission shows errors
- [ ] Database errors handled gracefully
- [ ] File upload errors displayed
- [ ] Navigation to non-existent claims handled

## User Experience
- [ ] TempData messages display correctly
- [ ] Success messages auto-hide
- [ ] Loading states on buttons
- [ ] Responsive design on mobile
- [ ] All navigation links work
- [ ] Logout functionality works

## Data Validation
- [ ] Required fields validated
- [ ] Email format validated
- [ ] Numeric fields validated
- [ ] Date fields validated
- [ ] Hourly rate restrictions enforced
- [ ] Hours worked range validated