import { Component, ReactNode } from "react";

// Keeps the vanilla UI alive if one of our components throws.
export class Guard extends Component<{ name: string; children?: ReactNode }, { failed: boolean }> {
    state = { failed: false };

    static getDerivedStateFromError() {
        return { failed: true };
    }

    componentDidCatch(error: unknown) {
        console.error(`[Unsquare] ${this.props.name} failed`, error);
    }

    render() {
        return this.state.failed ? null : this.props.children ?? null;
    }
}
