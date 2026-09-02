const endpointSelect = document.querySelector("#endpoint");
const minimumScoreInput = document.querySelector("#minimum-score");
const statusElement = document.querySelector("#status");
const outputElement = document.querySelector("#output");

async function request(path) {
	statusElement.textContent = `Requesting ${path}`;
	outputElement.textContent = "";

	const response = await fetch(path);
	const text = await response.text();
	let body = text;

	if (text) {
		try {
			body = JSON.parse(text);
		} catch {
			body = text;
		}
	}

	statusElement.textContent = `${response.status} ${response.statusText}`;
	outputElement.textContent =
		typeof body === "string" ? body : JSON.stringify(body, null, 2);

	return { response, body };
}

async function loadEndpoints() {
	const result = await request("/api/v1/endpoints");
	if (!result.response.ok || !Array.isArray(result.body)) {
		return;
	}

	endpointSelect.replaceChildren(
		...result.body.map(endpoint => {
			const option = document.createElement("option");
			option.value = endpoint.endpoint_id;
			option.textContent =
				`${endpoint.operating_system}: ${endpoint.endpoint_id}`;
			return option;
		}));

	statusElement.textContent = `Loaded ${result.body.length} endpoints`;
}

document.querySelector("#summary").addEventListener("click", () => {
	request(`/api/v1/summary/${endpointSelect.value}`);
});

document.querySelector("#recent-alerts").addEventListener("click", () => {
	request("/api/v1/alerts");
});

document.querySelector("#filtered-alerts").addEventListener("click", () => {
	const parameters = new URLSearchParams();
	if (endpointSelect.value) {
		parameters.set("endpoint_id", endpointSelect.value);
	}
	if (minimumScoreInput.value) {
		parameters.set("min_score", minimumScoreInput.value);
	}

	request(`/api/v1/alerts?${parameters}`);
});

loadEndpoints();
