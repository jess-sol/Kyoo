/*
 * Kyoo - A portable and vast media library solution.
 * Copyright (c) Kyoo.
 *
 * See AUTHORS.md and LICENSE file in the project root for full license information.
 *
 * Kyoo is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * any later version.
 *
 * Kyoo is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with Kyoo. If not, see <https://www.gnu.org/licenses/>.
 */

declare module "react-native-video" {
	interface VideoProperties {
		fonts?: Font[];
		onPlayPause: (isPlaying: boolean) => void;
		onMediaUnsupported?: () => void;
	}
	export type VideoProps = Omit<VideoProperties, "source"> & {
		source: { uri: string; hls: string };
	};
}

export * from "react-native-video";

import { Font } from "@kyoo/models";
import { IconButton, Menu } from "@kyoo/primitives";
import { ComponentProps, useRef } from "react";
import { atom, useAtom, useAtomValue, useSetAtom } from "jotai";
import NativeVideo, { OnLoadData } from "react-native-video";
import { useTranslation } from "react-i18next";
import { PlayMode, playModeAtom } from "./state";

const infoAtom = atom<OnLoadData | null>(null);
const videoAtom = atom(0);
const audioAtom = atom(0);

const Video = ({ onLoad, ...props }: ComponentProps<typeof NativeVideo>) => {
	const player = useRef<NativeVideo | null>(null);
	const setInfo = useSetAtom(infoAtom);
	const video = useAtomValue(videoAtom);
	const audio = useAtomValue(audioAtom);

	return (
		<NativeVideo
			ref={(ref) => {
				player.current = ref;
			}}
			onLoad={(info) => {
				setInfo(info);
				onLoad?.(info);
			}}
			selectedVideoTrack={video === -1 ? { type: "auto" } : { type: "resolution", value: video }}
			selectedAudioTrack={{ type: "index", value: audio }}
			{...props}
		/>
	);
};

export default Video;

type CustomMenu = ComponentProps<typeof Menu<ComponentProps<typeof IconButton>>>;
export const AudiosMenu = (props: CustomMenu) => {
	const info = useAtomValue(infoAtom);
	const [audio, setAudio] = useAtom(audioAtom);

	if (!info || info.audioTracks.length < 2) return null;

	return (
		<Menu {...props}>
			{info.audioTracks.map((x) => (
				<Menu.Item
					key={x.index}
					label={x.title}
					selected={audio === x.index}
					onSelect={() => setAudio(x.index)}
				/>
			))}
		</Menu>
	);
};

export const QualitiesMenu = (props: CustomMenu) => {
	const { t } = useTranslation();
	const info = useAtomValue(infoAtom);
	const [mode, setPlayMode] = useAtom(playModeAtom);
	const [video, setVideo] = useAtom(videoAtom);

	return (
		<Menu {...props}>
			<Menu.Item
				label={t("player.direct")}
				selected={mode == PlayMode.Direct}
				onSelect={() => setPlayMode(PlayMode.Direct)}
			/>
			<Menu.Item
				label={
					mode === PlayMode.Hls && video !== -1
						? `${t("player.auto")} (${video}p)`
						: t("player.auto")
				}
				selected={video === -1}
				onSelect={() => {
					setPlayMode(PlayMode.Hls);
					setVideo(-1);
				}}
			/>
			{/* TODO: Support video tracks when the play mode is not hls. */}
			{info?.videoTracks.map((x) => (
				<Menu.Item
					key={x.height}
					label={`${x.height}p`}
					selected={video === x.height}
					onSelect={() => {
						setPlayMode(PlayMode.Hls);
						setVideo(x.height);
					}}
				/>
			))}
		</Menu>
	);
};
