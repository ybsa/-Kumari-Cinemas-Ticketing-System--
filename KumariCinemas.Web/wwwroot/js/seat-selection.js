let currentShowId = 0;
let currentUnitPrice = 0;
let selectedSeats = [];
let bookedSeats = [];

async function showSeatMap(showId, unitPrice) {
    currentShowId = showId;
    currentUnitPrice = unitPrice;
    selectedSeats = [];

    // Fetch seat availability
    try {
        const response = await fetch(`/Ticket/GetSeatAvailability?showId=${showId}`);
        const data = await response.json();

        bookedSeats = data.bookedSeats || [];
        const capacity = data.capacity || 100;

        // Render seat map
        renderSeatMap(capacity);

        // Show modal
        document.getElementById('seatModal').style.display = 'flex';
    } catch (error) {
        alert('Error loading seat map. Please try again.');
        console.error(error);
    }
}

function renderSeatMap(capacity) {
    const seatMap = document.getElementById('seatMap');
    seatMap.innerHTML = '';

    // Calculate rows and columns (assuming 10 seats per row)
    const seatsPerRow = 10;
    const numRows = Math.ceil(capacity / seatsPerRow);

    for (let row = 0; row < numRows; row++) {
        const rowLetter = String.fromCharCode(65 + row); // A, B, C, ...

        for (let col = 1; col <= seatsPerRow; col++) {
            const seatNumber = `${rowLetter}${col}`;
            const seatElement = document.createElement('div');
            seatElement.className = 'seat';
            seatElement.setAttribute('data-seat', seatNumber);
            seatElement.title = seatNumber;

            // Check if seat is booked
            if (bookedSeats.includes(seatNumber)) {
                seatElement.classList.add('booked');
            } else {
                seatElement.classList.add('available');
                seatElement.onclick = () => toggleSeat(seatNumber);
            }

            seatMap.appendChild(seatElement);

            // Stop if we've reached capacity
            if (row * seatsPerRow + col >= capacity) break;
        }
    }

    updateSelectionSummary();
}

function toggleSeat(seatNumber) {
    const seatElement = document.querySelector(`[data-seat="${seatNumber}"]`);

    if (seatElement.classList.contains('booked')) {
        return; // Can't select booked seats
    }

    if (selectedSeats.includes(seatNumber)) {
        // Deselect
        selectedSeats = selectedSeats.filter(s => s !== seatNumber);
        seatElement.classList.remove('selected');
        seatElement.classList.add('available');
    } else {
        // Select
        selectedSeats.push(seatNumber);
        seatElement.classList.remove('available');
        seatElement.classList.add('selected');
    }

    updateSelectionSummary();
}

function updateSelectionSummary() {
    const seatsList = document.getElementById('selectedSeatsList');
    const totalPrice = document.getElementById('totalPrice');

    if (selectedSeats.length === 0) {
        seatsList.textContent = 'None';
        totalPrice.textContent = '0';
    } else {
        seatsList.textContent = selectedSeats.sort().join(', ');
        totalPrice.textContent = (selectedSeats.length * currentUnitPrice).toFixed(2);
    }
}

function confirmSeats() {
    if (selectedSeats.length === 0) {
        alert('Please select at least one seat.');
        return;
    }

    // Find the form for this show
    const form = document.querySelector(`form[data-show-id="${currentShowId}"]`);
    if (!form) {
        alert('Error: Could not find booking form.');
        return;
    }

    // Update form fields
    form.querySelector('.ticket-quantity').value = selectedSeats.length;
    form.querySelector('.selected-seats-input').value = selectedSeats.join(',');
    form.querySelector('.display-price').textContent = (selectedSeats.length * currentUnitPrice).toFixed(2);
    form.querySelector('.selected-count').textContent = selectedSeats.length;

    // Enable the book button
    const bookButton = form.querySelector('.btn-book');
    bookButton.disabled = false;

    // Close modal
    closeSeatModal();
}

function closeSeatModal() {
    document.getElementById('seatModal').style.display = 'none';
    selectedSeats = [];
}

// Close modal when clicking outside
document.getElementById('seatModal')?.addEventListener('click', function (e) {
    if (e.target === this) {
        closeSeatModal();
    }
});
